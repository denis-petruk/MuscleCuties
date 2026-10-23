# MuscleCuties exercise review import dry run

Source workbook SHA-256: `9de8cb8fe5739f148e8ff856b2dcd5b0cda939a9c7b3186cef8adbf7f7424c91`
Review manifest SHA-256: `76a9413f6c42d6c36b315c864caccb67d07a839a491d7d868124716bd10d8b29`

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
| Deployed PostgreSQL rows checked | No; no configured PostgreSQL connection in this workspace |
| Database changes applied | No |

Repository snapshot comparison: PASS.

## Before database apply

Run the `review-catalog-dry-run` database maintenance command against the deployed PostgreSQL catalog with the manifest SHA-256 above. It compares seed codes, names, movement patterns, equipment, setup, rep timing and existing contraindication flags. Any missing or changed row blocks import. Apply the new schema migration and then run `review-catalog-apply` only after that database dry run is reviewed. Both commands require the existing protected runner connection; no credentials belong in chat or in the mobile app.

The approved catalog records the owner’s direct instruction as the verification source for restriction assessments and all human review decisions. Existing contraindication flags still block exercises that conflict with a user’s active injury flags. The imported catalog will not enable automatic substitutions because pair conditions require the specified conditions to be satisfied.
