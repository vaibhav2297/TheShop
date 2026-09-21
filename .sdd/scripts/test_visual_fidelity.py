"""Isolated behavioral tests for visual gates; no app, Figma or database needed."""
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

from PIL import Image

spec = importlib.util.spec_from_file_location("visual_fidelity", Path(__file__).with_name("visual-fidelity.py"))
vf = importlib.util.module_from_spec(spec)
spec.loader.exec_module(vf)


class VisualGateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="sdd-visual-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        subprocess.run(["git", "init", "-q", str(self.root)], check=True)
        self.folder = self.root / ".specs/pilot"
        self.visual = self.folder / "visual"
        (self.visual / "reference").mkdir(parents=True)
        (self.visual / "actual").mkdir()
        (self.folder / "spec.md").write_text("## 6. Acceptance Criteria\n**AC-1:** Given a page, when rendered, then match design.\n")
        (self.folder / "plan.md").write_text("UI: src/TheShop.Web/Pages/Pilot.razor\n")
        source = self.root / "src/TheShop.Web/Pages/Pilot.razor"
        source.parent.mkdir(parents=True)
        source.write_text("<MudText>Pilot</MudText>\n")
        self.context = self.visual / "reference/context.md"
        self.context.write_text("Geometry: 10x10. Map white to ShopColors.Surface.\n")
        self.reference = self.visual / "reference/desktop.png"
        self.actual = self.visual / "actual/desktop.png"
        Image.new("RGB", (10, 10), "white").save(self.reference)
        shutil.copyfile(self.reference, self.actual)
        self.contract = {
            "version": 1, "feature": "pilot", "mode": "figma", "sourcePaths": [],
            "environment": {"browser": "chromium 140", "os": "test-os", "deviceScaleFactor": 1,
                            "colorScheme": "light", "reducedMotion": "reduce", "timezone": "UTC",
                            "fonts": "Test Sans 400; ready", "fixture": "pilot-v1"},
            "surfaces": [{"id": "desktop", "route": "/pilot", "state": "default", "locale": "en-CA",
                          "viewport": {"width": 10, "height": 10}, "acceptanceCriteria": ["AC-1"],
                          "figma": {"url": "https://www.figma.com/design/test/Pilot?node-id=1-2",
                                    "revision": "export-2026-09-21", "reference": "visual/reference/desktop.png",
                                    "referenceSha256": vf.digest(self.reference), "context": "visual/reference/context.md",
                                    "contextSha256": vf.digest(self.context)},
                          "checks": {key: "Measured expected value" for key in ("geometry", "typography", "assets", "states")},
                          "regions": [
                              {"id": "whole", "box": [0, 0, 10, 10], "pixelThreshold": 16,
                               "maxDiffRatio": .02, "rationale": "Fixture tolerance"},
                              {"id": "button", "box": [0, 0, 2, 2], "pixelThreshold": 16,
                               "maxDiffRatio": 0, "rationale": "Critical control must match"}]}]}
        self.save_contract()
        self.capture()

    def save_contract(self):
        vf.write(self.folder / "design-contract.json", self.contract)
        self.gate = vf.VisualGate(self.root, "pilot")

    def capture(self):
        fingerprint = self.gate.fingerprint()
        images = {"desktop": vf.digest(self.actual)}
        vf.write(self.visual / "capture.json", {"sourceFingerprint": fingerprint,
                 "environment": self.contract["environment"], "images": images})
        vf.write(self.visual / "review.json", {"sourceFingerprint": fingerprint, "images": images,
                 "surfaces": {"desktop": {key: {"passed": True, "observed": "Measured 10px; expected 10px"}
                              for key in ("geometry", "typography", "assets", "states")}}})

    def complete(self):
        for mode in ("plan", "align", "baseline", "regression", "verify"):
            self.gate.run(mode)

    def test_matching_images_complete_and_emit_diagnostics(self):
        self.complete()
        self.assertTrue((self.visual / "alignment-images/desktop-overlay.png").is_file())
        self.assertTrue((self.visual / "regression-images/desktop-diff.png").is_file())

    def test_critical_region_catches_difference_hidden_by_whole_frame(self):
        with Image.open(self.actual) as img:
            img.putpixel((0, 0), (0, 0, 0))
            img.save(self.actual)
        self.capture()
        with self.assertRaisesRegex(vf.GateError, "desktop/button"):
            self.gate.run("align")
        rows = vf.read(self.visual / "alignment.json")["regions"]
        self.assertTrue(rows[0]["passed"])
        self.assertFalse(rows[1]["passed"])

    def test_visual_failure_cannot_seed_baseline(self):
        Image.new("RGB", (10, 10), "black").save(self.actual)
        self.capture()
        with self.assertRaises(vf.GateError):
            self.gate.run("baseline")
        self.assertFalse((self.visual / "baseline").exists())

    def test_baseline_cannot_be_overwritten(self):
        self.complete()
        with self.assertRaisesRegex(vf.GateError, "replacement forbidden"):
            self.gate.run("baseline")

    def test_dirty_source_invalidates_evidence(self):
        self.complete()
        (self.root / "src/TheShop.Web/Pages/Pilot.razor").write_text("changed")
        with self.assertRaisesRegex(vf.GateError, "source is stale"):
            self.gate.run("verify")

    def test_generated_css_invalidates_evidence(self):
        self.complete()
        css = self.root / "src/TheShop.Web/wwwroot/css/TheShop.css"
        css.parent.mkdir(parents=True)
        css.write_text("body { color: red }")
        with self.assertRaisesRegex(vf.GateError, "source is stale"):
            self.gate.run("verify")

    def test_status_changes_do_not_invalidate_evidence(self):
        self.complete()
        (self.folder / "status.md").write_text("Verified")
        self.gate.run("verify")

    def test_reference_change_requires_new_contract_hash(self):
        self.complete()
        Image.new("RGB", (10, 10), "red").save(self.reference)
        with self.assertRaisesRegex(vf.GateError, "stale reference hash"):
            self.gate.run("verify")

    def test_contract_change_invalidates_baseline(self):
        self.complete()
        self.contract["surfaces"][0]["figma"]["revision"] = "new-export"
        self.save_contract()
        self.capture()
        with self.assertRaisesRegex(vf.GateError, "another contract"):
            self.gate.run("regression")

    def test_tampered_baseline_rejected(self):
        self.complete()
        Image.new("RGB", (10, 10), "black").save(self.visual / "baseline/desktop.png")
        with self.assertRaisesRegex(vf.GateError, "Baseline changed"):
            self.gate.run("regression")

    def test_changed_capture_requires_receipt(self):
        self.complete()
        Image.new("RGB", (10, 10), "black").save(self.actual)
        with self.assertRaisesRegex(vf.GateError, "image set/hash mismatch"):
            self.gate.run("verify")

    def test_wrong_environment_rejected(self):
        capture = vf.read(self.visual / "capture.json")
        capture["environment"]["browser"] = "different-browser"
        vf.write(self.visual / "capture.json", capture)
        with self.assertRaisesRegex(vf.GateError, "environment differs"):
            self.gate.run("align")

    def test_failed_semantic_review_rejected(self):
        review = vf.read(self.visual / "review.json")
        review["surfaces"]["desktop"]["typography"]["passed"] = False
        vf.write(self.visual / "review.json", review)
        with self.assertRaisesRegex(vf.GateError, "typography review missing/failed"):
            self.gate.run("align")

    def test_dimensions_cannot_be_resized_to_pass(self):
        Image.new("RGB", (9, 10), "white").save(self.actual)
        self.capture()
        with self.assertRaisesRegex(vf.GateError, "PNG dimensions"):
            self.gate.run("align")

    def test_path_escape_rejected(self):
        self.contract["surfaces"][0]["figma"]["reference"] = "../../outside.png"
        self.save_contract()
        with self.assertRaisesRegex(vf.GateError, "Path outside"):
            self.gate.run("plan")

    def test_missing_contract_fails_closed(self):
        with self.assertRaisesRegex(vf.GateError, "Missing"):
            vf.VisualGate(self.root, "missing")

    def test_unknown_ac_rejected(self):
        self.contract["surfaces"][0]["acceptanceCriteria"] = ["AC-99"]
        self.save_contract()
        with self.assertRaisesRegex(vf.GateError, "AC mapping"):
            self.gate.run("plan")

    def test_reference_must_be_png_at_plan_time(self):
        self.reference.write_text("not an image")
        self.contract["surfaces"][0]["figma"]["referenceSha256"] = vf.digest(self.reference)
        self.save_contract()
        with self.assertRaisesRegex(vf.GateError, "reference must be PNG"):
            self.gate.run("plan")

    def test_review_cannot_claim_missing_surface(self):
        review = vf.read(self.visual / "review.json")
        review["surfaces"] = {}
        vf.write(self.visual / "review.json", review)
        with self.assertRaisesRegex(vf.GateError, "coverage mismatch"):
            self.gate.run("align")

    def test_backend_skip_requires_consistent_declaration(self):
        self.contract = {"version": 1, "feature": "pilot", "mode": "backend", "reason": "Handler-only work"}
        self.save_contract()
        with self.assertRaisesRegex(vf.GateError, "conflicts with UI"):
            self.gate.run("verify")
        (self.folder / "plan.md").write_text("Application handler only")
        self.assertIn("SKIPPED", self.gate.run("verify"))

    def test_invalid_region_and_tolerance_rejected(self):
        region = self.contract["surfaces"][0]["regions"][0]
        for key, value in (("box", [0, 0, 11, 10]), ("maxDiffRatio", 1), ("pixelThreshold", 255)):
            original = region[key]
            region[key] = value
            self.save_contract()
            with self.assertRaises(vf.GateError):
                self.gate.run("plan")
            region[key] = original

    @unittest.skipUnless(shutil.which("pwsh"), "PowerShell not available")
    def test_powershell_dispatch_blocks_missing_visual_evidence(self):
        scripts = self.root / ".sdd/scripts"
        scripts.mkdir(parents=True)
        for name in ("check-sdd-gates.ps1", "visual-fidelity.py"):
            shutil.copyfile(Path(__file__).with_name(name), scripts / name)
        result = subprocess.run(["pwsh", "-NoProfile", "-File", str(scripts / "check-sdd-gates.ps1"),
                                 "visual", "-Feature", "pilot"], capture_output=True, text=True)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.complete()
        result = subprocess.run(["pwsh", "-NoProfile", "-File", str(scripts / "check-sdd-gates.ps1"),
                                 "visual", "-Feature", "pilot"], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    @unittest.skipUnless(shutil.which("pwsh"), "PowerShell not available")
    def test_ship_ready_requires_visuals_even_with_green_ledger(self):
        scripts = self.root / ".sdd/scripts"
        scripts.mkdir(parents=True)
        for name in ("check-sdd-gates.ps1", "visual-fidelity.py"):
            shutil.copyfile(Path(__file__).with_name(name), scripts / name)
        (self.folder / "status.md").write_text(
            "**Last updated:** 2026-09-21\n"
            "| 1. Spec | Confirmed | pass |\n| 2. Plan | Resolved | pass |\n"
            "| 3. Implement | Done | pass |\n| 4. Test | Passing | pass |\n"
            "| 5. Verify | Verified | pass |\n", encoding="utf-8")
        command = ["pwsh", "-NoProfile", "-File", str(scripts / "check-sdd-gates.ps1"),
                   "ship-ready", "-Feature", "pilot"]
        result = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("visual", result.stdout + result.stderr)
        self.complete()
        result = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
