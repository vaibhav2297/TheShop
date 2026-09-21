"""Validate Figma contracts and compare reproducible browser captures.

Pixel results are computed; browser provenance and semantic observations remain
declared evidence. This tool never launches a browser or rewrites a reference.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys


class GateError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise GateError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    require(path.is_file(), f"Missing {path}")
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def inside(root, relative):
    require(isinstance(relative, str) and relative, "Missing relative path")
    candidate = (root / relative).resolve()
    require(candidate.is_relative_to(root.resolve()) and candidate != root.resolve(),
            f"Path outside artifact root: {relative}")
    return candidate


def text(value, label):
    require(isinstance(value, str) and value.strip() and
            not re.search(r"REPLACE|EXACT_|TODO|TBD", value), f"Incomplete {label}")


def number(value, low, high, label):
    require(type(value) in (int, float) and math.isfinite(value) and low <= value <= high,
            f"Invalid {label}: {value}")


class VisualGate:
    def __init__(self, root, feature):
        require(re.fullmatch(r"(?:(?:00[1-9]|0[1-9][0-9]|[1-9][0-9]{2,})_)?[a-z0-9]+(?:-[a-z0-9]+)*", feature), "Unsafe feature name")
        self.root = Path(root).resolve()
        self.feature = feature
        self.folder = inside(self.root, f".specs/{feature}")
        self.contract_path = self.folder / "design-contract.json"
        self.contract = read(self.contract_path)
        self.visual = inside(self.folder, "visual")

    def plan(self):
        c = self.contract
        require(c.get("version") == 1 and c.get("feature") == self.feature,
                "Contract version/feature mismatch")
        require(c.get("mode") in ("backend", "figma"), "mode must be backend or figma")
        for name in ("spec.md", "plan.md"):
            require((self.folder / name).is_file(), f"Missing {name}")
        if c["mode"] == "backend":
            text(c.get("reason"), "backend reason")
            require(not c.get("surfaces"), "Backend cannot declare UI surfaces")
            plan = (self.folder / "plan.md").read_text(encoding="utf-8-sig")
            # Concrete UI paths or Figma links contradict a backend declaration.
            require(not re.search(r"https?://(?:www\.)?figma\.com/|\b[\w/-]+\.razor\b|src/TheShop\.Web/(?:Pages|Components|Styles|Theme)/", plan),
                    "Backend declaration conflicts with UI work in plan")
            return
        environment = c.get("environment", {})
        for key in ("browser", "os", "colorScheme", "reducedMotion", "timezone", "fonts", "fixture"):
            text(environment.get(key), f"environment.{key}")
        number(environment.get("deviceScaleFactor"), 1, 4, "deviceScaleFactor")
        require(isinstance(c.get("sourcePaths"), list), "sourcePaths must be an array")
        for path in c["sourcePaths"]:
            require(inside(self.root, path).is_file(), f"Missing source input {path}")
        surfaces = c.get("surfaces")
        require(isinstance(surfaces, list) and surfaces, "No visual surfaces")
        ids = set()
        spec = (self.folder / "spec.md").read_text(encoding="utf-8-sig")
        ac_ids = set(re.findall(r"\*\*AC-(\d+):\*\*", spec))
        for s in surfaces:
            sid = s.get("id", "")
            require(re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", sid) and sid not in ids,
                    f"Invalid/duplicate surface {sid}")
            ids.add(sid)
            for key in ("route", "state", "locale"):
                text(s.get(key), f"{sid}.{key}")
            viewport = s.get("viewport", {})
            for key in ("width", "height"):
                require(type(viewport.get(key)) is int, f"{sid}: integer viewport required")
                number(viewport[key], 1, 16000, f"{sid}.{key}")
            acs = s.get("acceptanceCriteria")
            require(isinstance(acs, list) and acs and all(
                isinstance(ac, str) and re.fullmatch(r"AC-\d+", ac) and ac[3:] in ac_ids for ac in acs),
                f"{sid}: unknown/missing AC mapping")
            f = s.get("figma", {})
            require(re.match(r"^https://(?:www\.)?figma\.com/(?:design|file)/[^?]+\?.*node-id=[\d:-]+", f.get("url", "")),
                    f"{sid}: exact Figma node URL required")
            text(f.get("revision"), f"{sid}.revision")
            for key in ("reference", "context"):
                path = inside(self.folder, f.get(key))
                require(path.is_file() and path.stat().st_size > 0, f"{sid}: missing {key}")
                require(digest(path) == f.get(key + "Sha256"), f"{sid}: stale {key} hash")
            with inside(self.folder, f["reference"]).open("rb") as stream:
                header = stream.read(24)
            require(len(header) == 24 and header[:8] == b"\x89PNG\r\n\x1a\n" and header[12:16] == b"IHDR",
                    f"{sid}: reference must be PNG")
            require(struct.unpack("!II", header[16:24]) == self.size(s),
                    f"{sid}: reference dimensions differ from viewport/scale")
            for key in ("geometry", "typography", "assets", "states"):
                text(s.get("checks", {}).get(key), f"{sid}.checks.{key}")
            regions = s.get("regions")
            require(isinstance(regions, list) and regions, f"{sid}: missing regions")
            width, height = self.size(s)
            names = set()
            for region in regions:
                rid = region.get("id", "")
                require(re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", rid) and rid not in names,
                        f"{sid}: invalid/duplicate region")
                names.add(rid)
                box = region.get("box")
                require(isinstance(box, list) and len(box) == 4 and all(type(n) is int for n in box),
                        f"{sid}/{rid}: invalid box")
                x1, y1, x2, y2 = box
                require(0 <= x1 < x2 <= width and 0 <= y1 < y2 <= height, f"{sid}/{rid}: box outside image")
                number(region.get("pixelThreshold"), 0, 64, "pixelThreshold (0..64)")
                number(region.get("maxDiffRatio"), 0, 0.02, "maxDiffRatio (0..0.02)")
                text(region.get("rationale"), f"{sid}/{rid}.rationale")
            require(any(r["box"] == [0, 0, width, height] for r in regions), f"{sid}: whole-frame region required")
            require(any(r["box"] != [0, 0, width, height] for r in regions),
                    f"{sid}: critical region required in addition to whole frame")

    def size(self, surface):
        scale = self.contract["environment"]["deviceScaleFactor"]
        values = [surface["viewport"][k] * scale for k in ("width", "height")]
        require(all(int(v) == v for v in values), "Non-integral PNG dimensions")
        return tuple(map(int, values))

    def fingerprint(self):
        # Git honors the project's ignore rules; include dirty/untracked inputs.
        result = subprocess.run(["git", "-C", str(self.root), "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
                                capture_output=True, check=True)
        paths = set()
        for raw in result.stdout.decode("utf-8").split("\0"):
            if not raw:
                continue
            p = Path(raw)
            if raw.startswith(("src/", "tests/", "supabase/")) or len(p.parts) == 1:
                paths.add(raw)
        paths.update(self.contract.get("sourcePaths", []))
        # Generated CSS can differ from SCSS when the build is stale.
        paths.add("src/TheShop.Web/wwwroot/css/TheShop.css")
        paths.update(f".specs/{self.feature}/{n}" for n in ("spec.md", "plan.md", "design-contract.json"))
        h = hashlib.sha256()
        for rel in sorted(paths):
            p = inside(self.root, rel)
            if p.is_file():
                h.update(rel.encode() + b"\0" + p.read_bytes() + b"\0")
        return h.hexdigest()

    def capture(self):
        capture = read(self.visual / "capture.json")
        require(capture.get("sourceFingerprint") == self.fingerprint(), "Capture source is stale; rebuild and recapture")
        require(capture.get("environment") == self.contract["environment"], "Capture environment differs from contract")
        hashes = {s["id"]: digest(inside(self.visual, f"actual/{s['id']}.png")) for s in self.contract["surfaces"]}
        require(capture.get("images") == hashes, "Capture image set/hash mismatch")
        return capture

    def review(self, capture):
        review = read(self.visual / "review.json")
        require(review.get("sourceFingerprint") == capture["sourceFingerprint"] and
                review.get("images") == capture["images"], "Visual review is stale")
        require(set(review.get("surfaces", {})) == set(capture["images"]), "Visual review surface coverage mismatch")
        for s in self.contract["surfaces"]:
            for key in ("geometry", "typography", "assets", "states"):
                check = review["surfaces"][s["id"]].get(key, {})
                require(check.get("passed") is True, f"{s['id']}: {key} review missing/failed")
                text(check.get("observed"), f"{s['id']}.{key}.observed")
        return digest(self.visual / "review.json")

    def pixels(self, kind, output=False):
        try:
            from PIL import Image, ImageChops
        except ImportError as exc:
            raise GateError("Pillow missing; install .sdd/scripts/visual-requirements.txt") from exc
        rows = []
        for s in self.contract["surfaces"]:
            sid = s["id"]
            reference = (inside(self.folder, s["figma"]["reference"]) if kind == "alignment"
                         else inside(self.visual, f"baseline/{sid}.png"))
            with Image.open(reference) as img:
                require(img.format == "PNG", f"{sid}: reference must be PNG")
                expected = img.convert("RGBA")
            with Image.open(inside(self.visual, f"actual/{sid}.png")) as img:
                require(img.format == "PNG", f"{sid}: capture must be PNG")
                actual = img.convert("RGBA")
            require(expected.size == actual.size == self.size(s), f"{sid}: PNG dimensions differ from viewport/scale")
            diff = ImageChops.difference(expected, actual)
            bands = diff.split()
            magnitude = bands[0]
            for band in bands[1:]:
                magnitude = ImageChops.lighter(magnitude, band)
            if output:
                folder = inside(self.visual, kind + "-images")
                folder.mkdir(parents=True, exist_ok=True)
                Image.blend(expected, actual, 0.5).save(folder / f"{sid}-overlay.png")
                # Show RGB and alpha differences on an opaque diagnostic image.
                magnitude.convert("RGB").save(folder / f"{sid}-diff.png")
            for region in s["regions"]:
                crop = magnitude.crop(tuple(region["box"]))
                changed = sum(count for value, count in enumerate(crop.histogram())
                              if value > region["pixelThreshold"])
                ratio = changed / (crop.width * crop.height)
                rows.append({"surface": sid, "region": region["id"], "changedPixels": changed,
                             "diffRatio": ratio, "passed": ratio <= region["maxDiffRatio"]})
        return rows

    def baseline_manifest(self):
        manifest = read(self.visual / "baseline" / "manifest.json")
        require(manifest.get("contractSha256") == digest(self.contract_path), "Baseline belongs to another contract")
        alignment = manifest.get("alignment", {})
        require(alignment.get("passed") is True and alignment.get("contractSha256") == digest(self.contract_path),
                "Baseline lacks passing Figma alignment provenance")
        hashes = {s["id"]: digest(inside(self.visual, f"baseline/{s['id']}.png")) for s in self.contract["surfaces"]}
        require(manifest.get("images") == hashes == alignment.get("images"), "Baseline changed since alignment")
        return manifest

    def report(self, kind, output=False):
        capture = self.capture()
        review_hash = self.review(capture) if kind == "alignment" else None
        baseline_hash = None
        if kind == "regression":
            self.baseline_manifest()
            baseline_hash = digest(self.visual / "baseline" / "manifest.json")
        rows = self.pixels(kind, output)
        return {"kind": kind, "contractSha256": digest(self.contract_path),
                "sourceFingerprint": capture["sourceFingerprint"], "images": capture["images"],
                "reviewSha256": review_hash, "baselineSha256": baseline_hash,
                "regions": rows, "passed": all(row["passed"] for row in rows)}

    def run(self, mode):
        self.plan()
        if mode == "fingerprint":
            return self.fingerprint()
        if self.contract["mode"] == "backend":
            return "SKIPPED: declared backend-only"
        if mode == "plan":
            return "PASS: design contract"
        if mode in ("align", "regression"):
            kind = "alignment" if mode == "align" else mode
            report = self.report(kind, output=True)
            write(inside(self.visual, f"{kind}.json"), report)
            failed = [f"{r['surface']}/{r['region']} ({r['diffRatio']:.4%})" for r in report["regions"] if not r["passed"]]
            require(report["passed"], "Visual differences: " + ", ".join(failed))
        elif mode == "baseline":
            current = self.report("alignment")
            require(current["passed"] and read(self.visual / "alignment.json") == current,
                    "Fresh passing Figma alignment required before baseline")
            target = inside(self.visual, "baseline")
            require(not target.exists(), "Baseline exists; automatic replacement forbidden")
            target.mkdir()
            for s in self.contract["surfaces"]:
                shutil.copyfile(inside(self.visual, f"actual/{s['id']}.png"), target / f"{s['id']}.png")
            write(target / "manifest.json", {"contractSha256": digest(self.contract_path),
                                            "images": current["images"], "alignment": current})
        elif mode == "verify":
            for kind in ("alignment", "regression"):
                current = self.report(kind)
                require(current["passed"] and read(self.visual / f"{kind}.json") == current,
                        f"{kind} missing, failed or stale; recapture and rerun")
        return f"PASS: {mode}"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("plan", "fingerprint", "align", "baseline", "regression", "verify"))
    parser.add_argument("--feature", required=True)
    args = parser.parse_args()
    try:
        print(VisualGate(Path(__file__).resolve().parents[2], args.feature).run(args.mode))
        return 0
    except (GateError, OSError, ValueError, KeyError, TypeError, AttributeError, subprocess.CalledProcessError) as exc:
        print(f"[visual-fidelity] FAIL: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
