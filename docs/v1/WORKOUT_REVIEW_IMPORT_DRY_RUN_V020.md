# MuscleCuties exercise review import dry run

Source workbook SHA-256: `9de8cb8fe5739f148e8ff856b2dcd5b0cda939a9c7b3186cef8adbf7f7424c91`
Review manifest SHA-256: `399a3237475deb01f2189a0eb99989635107e11aa068f9efd1c74a24ae920cc6`

| Check | Result |
| --- | --- |
| Reviewed exercise records | 59 |
| Matched canonical repository records | 59 |
| Missing or changed repository records | 0 |
| Directed pair reviews | 160 |
| Close programming alternatives | 70 |
| Manual alternatives requiring goal, slot, or dose changes | 79 |
| Rejected automatic alternatives | 11 |
| Template slot reviews | 52 |
| Exercise restriction assessments | 59 assessed and cleared subject to existing contraindication flags |
| Direct instruction verification | 59 restriction assessments, 160 directed pairs, and 52 template slots verified by the owner |
| Reviewer attribution | Confirmed by owner direct instruction in this chat |
| Automatic substitutes enabled by import | 0 |
| Deployed PostgreSQL rows checked | No; no deployed connection was configured or used |
| Local disposable PostgreSQL rows checked | Yes; 59/59 matched after canonical seed |
| Local reviewed metadata applied | Yes; clean dry run, 0 issues, automatic substitutions remain disabled |
| Deployed database changes applied | No |

Repository snapshot comparison: PASS.

## Before database apply

The local disposable PostgreSQL 17 database on `127.0.0.1:54329` has the schema, canonical seed, and reviewed metadata applied. Its migration and import checks passed. To compare a deployed catalog, run `review-catalog-dry-run` with the manifest SHA-256 above through the protected runner connection. It compares seed codes, names, movement patterns, equipment, setup, rep timing and existing contraindication flags. Any missing or changed row blocks import. Apply the schema migration and run `review-catalog-apply` only after the deployed dry run is reviewed. No deployed connection or credentials were used during the local setup.

The approved catalog records the owner’s direct instruction as the verification source for restriction assessments and all human review decisions. Existing contraindication flags still block exercises that conflict with a user’s active injury flags. The imported catalog will not enable automatic substitutions because pair conditions require the specified conditions to be satisfied.
