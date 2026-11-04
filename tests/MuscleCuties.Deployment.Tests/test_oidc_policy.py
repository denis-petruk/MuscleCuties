"""Static policy verification and negative tests for GitHub Actions OIDC identity."""

import unittest
from pathlib import Path


class OidcPolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        template_path = Path(__file__).resolve().parents[2] / "infra" / "cloudformation" / "identity.yaml"
        cls.template_text = template_path.read_text(encoding="utf-8")

    def test_oidc_provider_configured(self):
        self.assertIn("https://token.actions.githubusercontent.com", self.template_text)
        self.assertIn("sts.amazonaws.com", self.template_text)

    def test_artifact_upload_role_scoped_to_main_branch(self):
        self.assertIn("GitHubActionsArtifactUploadRole", self.template_text)
        self.assertIn("ref:refs/heads/main", self.template_text)
        self.assertIn("s3:PutObject", self.template_text)

    def test_staging_deploy_role_scoped_to_staging_environment(self):
        self.assertIn("GitHubActionsStagingDeployRole", self.template_text)
        self.assertIn("environment:staging", self.template_text)

    def test_beta_deploy_role_scoped_to_beta_environment(self):
        self.assertIn("GitHubActionsBetaDeployRole", self.template_text)
        self.assertIn("environment:beta", self.template_text)

    def test_staging_role_has_no_beta_resources(self):
        staging_section = self.template_text.split("GitHubActionsBetaDeployRole:")[0]
        self.assertIn("GitHubActionsStagingDeployRole:", staging_section)
        # Verify no Beta function is referenced in staging section
        self.assertNotIn("MuscleCuties-Server-Beta", staging_section)
        self.assertNotIn("MuscleCuties-Reconciliation-Beta", staging_section)


if __name__ == "__main__":
    unittest.main()
