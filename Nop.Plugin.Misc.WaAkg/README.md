# WhatsApp Automation Suite (WA-AKG) — nopCommerce 4.90 plugin

Transactional + campaign WhatsApp messaging for **The Malas Jewelry**, driven by a self-hosted
WA-AKG gateway. Every call is server-to-server, so the API key never reaches the browser and
there is no CORS involved.

System name: `Misc.WaAkg` · Target framework: `net9.0` · Supported version: `4.90`

---

## 1. Install

1. Copy the folder to `src/Plugins/Nop.Plugin.Misc.WaAkg`.
2. Add it to the solution (optional) and build:

   ```bash
   dotnet build src/Plugins/Nop.Plugin.Misc.WaAkg/Nop.Plugin.Misc.WaAkg.csproj
   ```

   Output auto-copies to `src/Presentation/Nop.Web/Plugins/Misc.WaAkg`.
3. Restart the site → **Admin > Configuration > Local plugins** → Install
   *WhatsApp Automation Suite (WA-AKG)*.
   Installation runs the FluentMigrator migration that creates `QueuedWhatsAppMessage`
   and registers two scheduled tasks.
4. **Admin > Configuration > Local plugins > Configure** → Connection tab →
   **Test connection** must go green.
5. Place a test order and confirm the message arrives.

## 2. What ships

| Layer | Files |
|---|---|
| Plugin | `WaAkgPlugin.cs`, `WaAkgSettings.cs`, `WaAkgDefaults.cs`, `plugin.json` |
| Infrastructure | `Infrastructure/DependencyRegistrar.cs`, `Infrastructure/RouteProvider.cs` |
| Data | `Domain/QueuedWhatsAppMessage.cs`, `Data/SchemaMigration.cs` |
| Services | `IWaAkgService` / `WaAkgService` (gateway client), `IWaAkgQueueService` / `WaAkgQueueService` (queue + throttling), `IWaAkgTokenService` / `WaAkgTokenService` (tokens) |
| Events | `OrderPlacedConsumer`, `OrderPaidConsumer`, `ShipmentCreatedConsumer`, `OrderStatusChangedConsumer`, `CustomerRegisteredConsumer` |
| Tasks | `ProcessQueueTask` (every 60s), `CartReminderTask` (every 6h) |
| Admin | `Controllers/WaAkgAdminController.cs`, `Views/Configure.cshtml`, `Models/*` |
| Storefront | `Components/WaAkgOptInViewComponent.cs`, `Controllers/WaAkgPublicController.cs`, `Views/Shared/Components/WaAkgOptIn/Default.cshtml` |

## 3. How messages flow

Consumers never call the gateway. They token-replace a template and **insert a queue row**
whose `ScheduledOnUtc` is placed one `SendDelaySeconds` gap after the last pending row, then
pushed out of the quiet window if needed. `ProcessQueueTask` picks up at most 25 due rows per
run and sends them sequentially, updating `SentOnUtc` / `Retries` / `LastError`.

Gateway contract used:

**Verified against the gateway's own `GET {base}/api/docs` (OpenAPI, v1.2.0)** — the contract in
the original build spec was wrong on two points (media route, message shape) and has been
corrected here:

| Purpose | Call |
|---|---|
| Health / sessions | `GET  {base}/api/sessions` |
| Text / image / video / document / sticker | `POST {base}/api/messages/{sessionId}/{jid}/send` — universal endpoint, JSON body |
| Broadcast | `POST {base}/api/messages/{sessionId}/broadcast` — JSON `{ recipients[], message, delay }` |

The universal `/send` endpoint takes `"message"` as a **nested object**, never a bare string:

```json
// text
{ "message": { "text": "Hello!" } }

// image / video (attachment is a URL, not an upload)
{ "message": { "image": { "url": "https://.../pic.jpg" }, "caption": "..." } }

// document
{ "message": { "document": { "url": "https://.../file.pdf" }, "caption": "...", "fileName": "file.pdf" } }
```

**There is no multipart/form-data upload route and no separate `/media` endpoint** — every
attachment must already be reachable at a public http(s) URL. A local file path passed as
`MediaUrl` is rejected by the plugin with a clear error rather than silently failing at the
gateway. If you need to attach files that only exist on the nopCommerce server, upload them
somewhere public first (media server, Google Drive public link, S3, `/content/images/...` on the
store itself) and queue the resulting URL.

Success = HTTP 2xx **and** `status != false` in the envelope.
JID = `{digits}@s.whatsapp.net`; 10-digit numbers get the configured country code prefixed.
`SessionId` in settings must be the gateway's `sessionId` field (e.g. `5w7e27`), not its internal
database `id` (the long `cmu...` string) and not the session `name`.

## 4. Opt-in

The checkbox is delivered as a **widget** rather than a view override, so no core `.cshtml`
is touched. Zones used (see `GetWidgetZonesAsync`):

```
checkout_billing_address_bottom
op_checkout_billing_address_bottom
checkout_confirm_top
```

If your theme uses different zone names, change that list — it is plain strings on purpose.
The checkbox POSTs to `/wa-akg/save-opt-in`, which stores the generic attribute
`WaAkgOptIn` = `true|false` on the customer. All consumers honour it when
`RequireOptIn` is on.

## 5. Anti-ban behaviour

- `SendDelaySeconds` gap enforced both at enqueue time and inside the send loop.
- `DailySendLimit` checked against messages actually delivered in the last rolling 24h.
- Quiet window `QuietHourStart` → `QuietHourEnd` in **store time**; rows created inside it are
  deferred to the resume hour. Set start == end to disable.
- Failed rows back off 5 minutes × retry count, then stop at `MaxRetries` and wait for a manual
  Retry from the Queue tab.

## 6. Things to check against your exact 4.90.6 source

Three APIs moved around in recent nopCommerce releases. If the build complains, these are the
only lines to touch:

1. `Controllers/WaAkgAdminController.cs` → `StandardPermission.Configuration.MANAGE_PLUGINS`.
   On older trees this is `StandardPermissionProvider.ManagePlugins`.
2. `Controllers/WaAkgAdminController.cs` → `[Area("Admin")]` is used instead of
   `[Area(AreaNames.Admin)]` / `[Area(AreaNames.ADMIN)]`, which changed casing between versions.
   Swap it in if you prefer the constant.
3. `Tasks/*.cs` → `Nop.Services.ScheduleTasks`. Pre-4.60 trees use `Nop.Services.Tasks`.

## 7. appsettings snippet (optional)

Nothing is required in `appsettings.json` — everything lives in plugin settings. If you want a
longer outbound timeout for slow links, this is the only knob worth touching:

```json
{
  "DistributedCacheConfig": {
    "Enabled": false
  },
  "CommonConfig": {
    "ScheduleTaskRunTimeout": 180000,
    "UseSessionStateTempDataProvider": false
  }
}
```

`ScheduleTaskRunTimeout` must comfortably exceed `BATCH_SIZE × SendDelaySeconds`
(25 × 10s = 250s by default), otherwise long queue runs get cut short.

## 8. Acceptance checklist

| # | Test | Expected |
|---|---|---|
| T1 | Test connection with a wrong key | red, "API key invalid" |
| T2 | Prepaid order placed | OrderPlaced message within ~2 min |
| T3 | COD order placed | OrderPlaced + CodAdvance |
| T4 | Order marked paid | OrderPaid |
| T5 | Shipment created | Shipment with tracking number |
| T6 | Status → Complete / Cancelled | Delivered / Cancelled |
| T7 | New registration | Welcome |
| T8 | Broadcast with media URL to 3 numbers | 3 queued rows, all delivered with the image |
| T9 | Gateway stopped | rows retry to `MaxRetries`, hold with `LastError`; manual Retry delivers after restart |
| T10 | Row created at 23:00 store time | `ScheduledOnUtc` lands at 08:00 next day |

## 9. Compliance note

This talks to an unofficial WhatsApp session, not the WhatsApp Business Platform. Automated
sending on a personal/business app session is against WhatsApp's terms and the number can be
banned without warning or appeal — order updates to people who opted in are much lower risk
than campaigns. If broadcasting becomes a real channel for the store, moving the marketing
templates to the official Cloud API is the durable option; the queue and template layers here
would not need to change, only `WaAkgService`.

## Module B v2 – WhatsApp OTP Popup Login

Adds a checkout-only OTP gate for guest customers, layered on top of the plugin's existing
`SendTextAsync`, queue-free (send happens synchronously so the OTP arrives before the modal's
"Send OTP" click finishes).

### What it does

1. A guest lands on `/checkout` (any step, including via the DirectCheckout plugin's guest-page
   bypass) with `EnableWhatsAppOtp` on. A non-dismissible full-screen modal appears — same page,
   no redirect, body scroll locked.
2. Step 1: phone number → `POST /wa-otp/send`. A 6-digit OTP is generated, cached in memory for
   `OtpExpiryMinutes`, and sent over WhatsApp via the existing gateway client.
3. Step 2: six auto-advancing OTP boxes (paste-to-fill supported) → auto-verifies on the 6th
   digit, or via the Confirm button → `POST /wa-otp/verify`.
4. On the correct OTP:
   - **First time on this number**: the guest's own (already-populated-with-cart) customer
     record is registered in place via `ICustomerRegistrationService.RegisterAsync`, using the
     phone as username and a random 12-character temp password. `MustChangePassword` is stored
     as a generic attribute, a WhatsApp-only placeholder address/email is attached, and the temp
     password is sent over WhatsApp.
   - **Returning number**: the existing customer (looked up by phone-as-username) is reused.
   - Either way, the customer is signed in with `IAuthenticationService.SignInAsync`, the modal
     closes, and the page reloads into the now-authenticated checkout — cart intact, since it's
     the same customer record throughout.
5. Later visits: `WaAkgMustChangeFilter` redirects the customer to `/customer/changepassword`
   the moment they touch any other My Account page, but never interrupts Cart or Checkout.
6. `WaAkgCheckoutGateFilter` is the server-side backstop: any guest POST straight into the
   Checkout controller (curl, stale tab, devtools) gets a 403 JSON response while the setting
   is on, GETs are always allowed through so the page — and the modal on top of it — can render.

### Settings (new "WhatsApp OTP Login" tab)

`EnableWhatsAppOtp`, `OtpMessageTemplate` (%OTP%, %ExpiryMin%), `OtpPasswordTemplate`
(%PASSWORD%, %StoreUrl%), `OtpExpiryMinutes`, `OtpMaxAttempts`, `OtpResendCooldownSec`.

### New files

`Controllers/WaOtpController.cs`, `Infrastructure/WaAkgCheckoutGateFilter.cs`,
`Infrastructure/WaAkgMustChangeFilter.cs`, `Components/WaOtpWidget.cs`,
`Views/Shared/Components/WaOtpWidget/Default.cshtml`, `Models/WaOtpModels.cs`.

### Compatibility note re: DirectCheckout plugin

The DirectCheckout plugin (`Nop.Plugin.Misc.DirectCheckout`) already redirects guests straight
to `/checkout`, bypassing `/login/checkoutasguest` entirely. This is exactly why the OTP modal
renders on `/checkout` itself via a widget zone (`main_page_after_header`) rather than on the
guest-checkout page — the latter is never hit when DirectCheckout's `SkipCheckoutGuestPage`
setting is on.

### Known limitation

OTP/cooldown/attempt state lives in `IMemoryCache` (single-process). On a web-farm deployment
with multiple app instances behind a load balancer without sticky sessions, a resend or verify
request could land on a different instance than the one that issued the OTP. Fine for a
single-server deployment; flag if a farm setup is ever planned so this can move to a shared
store (e.g. `IStaticCacheManager`/distributed cache) instead.
