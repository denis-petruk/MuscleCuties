# Registration email deliverability

Checked 2026-10-22 for `musclecuties.app`. The production server sends registration and recovery codes through Resend from `MuscleCuties <noreply@musclecuties.app>`. A Resend test message was marked **Delivered**, which confirms acceptance by the recipient server, not inbox placement. Its Deliverability Insights initially listed **No DMARC record found** and **Don't use "no-reply"**.

**DMARC correction completed 2026-10-22:** Cloudflare now has one `_dmarc` TXT record with `v=DMARC1; p=none;`. Both authoritative Cloudflare nameservers and a public resolver returned that value. Resend now lists **Include valid DMARC record** under **Doing great**, and the checked registration message has one remaining **Needs attention** item: **Don't use "no-reply"**. Resend lists its DKIM and SPF records as **Verified**.

**Sender decision (2026-10-22):** The owner chose to keep `noreply@musclecuties.app` for now. Resend's remaining sender warning is accepted for this round of testing. Do not change `Resend__FromEmail` or the production default until the owner chooses a monitored reply destination.

**Controlled send attempt (2026-10-22):** The owner authorized one synthetic registration challenge to the previously used controlled mailbox. The live registration endpoint returned HTTP 429 at about 11:17 a.m. Toronto time before delivery because that address had reached the application's five-challenges-per-rolling-day limit. No new message was sent by this attempt. A conservative retry is **no earlier than 11:20 a.m. Toronto time on 2026-10-23**, when all earlier attempts must be outside the 24-hour window. Do not bypass or raise the production rate limit for a deliverability test.

**Registration screen report (2026-10-22, 11:35 a.m. Toronto time):** The app displayed the literal `serverError` before OTP. This is consistent with the same rate limit: `MapChallengeStart` returns an empty HTTP 429 for `RateLimited` or `Cooldown`; `MuscleCutiesBackendApiClient.MapResponseAsync` maps an unrecognized non-success status with no error body to `BackendApiStatus.ServerError` and `errorCode="serverError"`; the registration gateway passes that string to the screen. The specific device request's HTTP status was not captured, so this remains a strong explanation rather than a confirmed trace. The existing Resend domain and DNS checks do not indicate a provider failure. Give 429 a stable rate-limit error code and a readable message in the client when the registration UI owner updates this flow.

## Public DNS findings

| Record | Observed result | Meaning |
| --- | --- | --- |
| `_dmarc.musclecuties.app` TXT | `v=DMARC1; p=none;` | Published and independently verified; keep one policy record. |
| `resend._domainkey.musclecuties.app` TXT | Resend public key resolves | DKIM material is published. Confirm signature pass and `d=musclecuties.app` in a received message. |
| `send.musclecuties.app` CNAME | `send.forge.rmta.net` | Resend return path resolves to its SPF and bounce handling records. |
| `rsend.musclecuties.app` CNAME | `rsend.forge.rmta.net` | Resend tracking host resolves. |
| `musclecuties.app` TXT / MX / A / AAAA | No answer | The root has no SPF policy, public inbound mail route, or reachable website. Do not invent a root SPF record for Resend's `send` return path or switch to a reply-capable sender before a real mailbox exists. |

The DNS nameservers are Cloudflare. Public DNS alone cannot prove that a specific received message passed SPF, DKIM, or DMARC; check its original headers after the DNS change.

## Finish the fix

1. Use a controlled mailbox to inspect original headers on a **new** registration message after the DNS change. Confirm `dkim=pass`, `spf=pass`, and `dmarc=pass`, with `header.from=musclecuties.app` and an aligned DKIM `d=` or SPF return-path domain. Resend's verified domain and public records alone do not prove the result for each received message.
2. Compare inbox placement at the affected provider. Record the Resend message status and Insights, authentication results, and mailbox folder without storing the recipient address or one-time code in logs. If authentication passes and placement remains spam, review the provider's sender reputation and complaint data; DNS alone cannot guarantee inbox placement.
3. Keep `p=none` while all legitimate sending sources are reviewed. Add a `rua` address only after a mailbox or DMARC report service is configured to receive reports. Consider `p=quarantine` or `p=reject` only after authentic mail flow is confirmed.
4. Publish the finalized public `musclecuties.app` website, bilingual policy/support pages, and AASA file. The static site (`index.html`, `privacy.html`, `terms.html`, `support.html`, `.well-known/apple-app-site-association`, `_headers`) is ready in `services/static/`, with explicit documentation of Resend as the transactional OTP email processor, active support contact (`deniska.petruk@icloud.com`), and 1–2 business day response times. Deploying this provides public domain identity and reputation context.
5. If the owner later chooses a monitored mailbox, test incoming replies first, then change `Resend__FromEmail` on Lambda and the checked-in production default together. Send a synthetic registration and recovery message and verify replies arrive. An address without a working mailbox would mislead recipients.

Resend's [DMARC guidance](https://resend.com/docs/dashboard/domains/dmarc) recommends `p=none` as a starting policy; [Deliverability Insights](https://resend.com/docs/dashboard/emails/deliverability-insights) checks DMARC and `no-reply` usage, and its [new-domain guide](https://resend.com/guides/email/new-domain-emails-going-to-spam) discusses reputation and website signals. [Cloudflare's DNS instructions](https://developers.cloudflare.com/dns/manage-dns-records/how-to/create-dns-records/) describe adding the TXT record. [Google's sender guidance](https://support.google.com/mail/answer/81126) explains authentication and alignment requirements.
