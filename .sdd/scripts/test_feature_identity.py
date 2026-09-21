"""Feature numbering, lookup and backward-compatibility tests."""
from pathlib import Path
import subprocess
import shutil
import tempfile
import unittest

from feature_identity import inventory, next_id, normalize, resolve


VALID_SPEC = '''# Pilot
**Feature:** `{feature}`
## 1. Problem Statement
**Solution (one line):** Pilot.
### Scope
**In scope:** Pilot.
**Out of scope:** Everything else.
### Actors & Access
Public.
## 2. Functional Requirements
**FR-1:** Pilot.
## 3. Functional Behaviors
### Behavior 1: Pilot
Works.
## 4. Constraints
### Business Rules
None.
## 5. Edge Cases & Error Handling
**Edge case:** Missing. **User experience:** Message.
## 6. Acceptance Criteria
**AC-1:** Given pilot, when opened, then works.
## Assumptions & Open Questions
None.
---
**Status:** Confirmed
**Created:** 2026-09-21
'''


class FeatureIdentityTests(unittest.TestCase):
    def test_first_number_ignores_legacy_count(self):
        self.assertEqual(next_id("Manage Products", {"old-feature", "another"}), "001_manage-products")

    def test_next_uses_max_not_count_or_gap(self):
        self.assertEqual(next_id("Checkout", {"001_cart", "004_payment"}), "005_checkout")

    def test_explicit_next_number(self):
        self.assertEqual(next_id("005_Check Out", {"004_cart"}), "005_check-out")
        with self.assertRaisesRegex(ValueError, "not next"):
            next_id("002_cart", set())

    def test_padding_and_overflow(self):
        self.assertEqual(next_id("next", {"999_previous"}), "1000_next")
        for value in ("000_bad", "01_bad", "0001_bad", "../bad", "a/b", "a\\b", "bad&name"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                normalize(value)

    def test_resolve_preserves_number_and_legacy(self):
        self.assertEqual(resolve("005_Checkout.md", {"005_checkout"}), "005_checkout")
        self.assertEqual(resolve("Checkout", {"005_checkout"}), "005_checkout")
        self.assertEqual(resolve("checkout", {"checkout", "005_checkout"}), "checkout")

    def test_unknown_or_ambiguous_fails(self):
        for value, names in (("missing", set()), ("001_cart", {"002_cart"}),
                             ("cart", {"001_cart", "002_cart"})):
            with self.subTest(value=value), self.assertRaises(ValueError):
                resolve(value, names)

    def test_existing_feature_cannot_get_second_number(self):
        with self.assertRaisesRegex(ValueError, "already exists"):
            next_id("cart", {"004_cart"})

    def test_number_collision_fails(self):
        names = {"005_cart", "005_payment"}
        with self.assertRaisesRegex(ValueError, "collision"):
            next_id("checkout", names)
        with self.assertRaisesRegex(ValueError, "collision"):
            resolve("005_cart", names)

    def test_inventory_includes_folders_local_and_remote_branches(self):
        with tempfile.TemporaryDirectory(prefix="sdd-identity-") as tmp:
            root = Path(tmp)
            subprocess.run(["git", "init", "-q", tmp], check=True)
            subprocess.run(["git", "-C", tmp, "-c", "user.name=Test", "-c", "user.email=test@example.invalid",
                            "commit", "--allow-empty", "-qm", "fixture"], check=True)
            (root / ".specs/002_cart").mkdir(parents=True)
            subprocess.run(["git", "-C", tmp, "branch", "feature/004_checkout"], check=True)
            subprocess.run(["git", "-C", tmp, "update-ref", "refs/remotes/origin/feature/007_payment", "HEAD"], check=True)
            names = inventory(root)
            self.assertEqual(names, {"002_cart", "004_checkout", "007_payment"})
            self.assertEqual(next_id("shipping", names), "008_shipping")
            self.assertFalse((root / ".specs/008_shipping").exists())

    @unittest.skipUnless(shutil.which("pwsh"), "PowerShell not available")
    def test_spec_gate_numbering_and_legacy_compatibility(self):
        with tempfile.TemporaryDirectory(prefix="sdd-number-gate-") as tmp:
            root = Path(tmp)
            subprocess.run(["git", "init", "-q", tmp], check=True)
            scripts = root / ".sdd/scripts"
            scripts.mkdir(parents=True)
            shutil.copyfile(Path(__file__).with_name("check-sdd-gates.ps1"), scripts / "check-sdd-gates.ps1")
            shutil.copyfile(Path(__file__).with_name("visual-fidelity.py"), scripts / "visual-fidelity.py")
            for feature in ("005_pilot", "legacy", "new-unnumbered"):
                folder = root / ".specs" / feature
                folder.mkdir(parents=True)
                (folder / "spec.md").write_text(VALID_SPEC.format(feature=feature), encoding="utf-8")
            subprocess.run(["git", "-C", tmp, "add", ".specs/legacy/spec.md"], check=True)
            subprocess.run(["git", "-C", tmp, "-c", "user.name=Test", "-c", "user.email=test@example.invalid",
                            "commit", "-qm", "legacy fixture"], check=True)
            # Staging a new unnumbered spec must not grandfather it.
            subprocess.run(["git", "-C", tmp, "add", ".specs/new-unnumbered/spec.md"], check=True)
            command = ["pwsh", "-NoProfile", "-File", str(scripts / "check-sdd-gates.ps1"), "spec", "-Feature"]
            for feature, expected in (("005_pilot", 0), ("legacy", 0), ("new-unnumbered", 1), ("../escape", 1)):
                result = subprocess.run(command + [feature], capture_output=True, text=True)
                self.assertEqual(result.returncode, expected, result.stdout + result.stderr)
            numbered = root / ".specs/005_pilot/spec.md"
            numbered.write_text(VALID_SPEC.format(feature="006_wrong"), encoding="utf-8")
            result = subprocess.run(command + ["005_pilot"], capture_output=True, text=True)
            self.assertEqual(result.returncode, 1)
            self.assertIn("matching **Feature:**", result.stdout + result.stderr)
            # Numbered IDs must also reach the visual gate and backend skip.
            import json
            folder = root / ".specs/005_pilot"
            (folder / "plan.md").write_text("Handler-only work", encoding="utf-8")
            (folder / "design-contract.json").write_text(json.dumps({"version": 1, "feature": "005_pilot",
                "mode": "backend", "reason": "Handler-only work"}), encoding="utf-8")
            result = subprocess.run(command[:-2] + ["visual", "-Feature", "005_pilot"], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
