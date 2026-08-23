-- Upsert free-tier channel subscribe reminder email template (deep link)
-- Row was missing on prod (UPDATE matched 0 rows).

SELECT "Id", "Name", "Subject", LEFT("BodyHtml", 80) AS body_preview
FROM xgb_dashopnvpn."EmailBroadcastTemplates"
WHERE "Name" ILIKE '%free_tier%' OR "Name" ILIKE '%channel%' OR "Name" ILIKE '%subscribe%'
ORDER BY "Id";

INSERT INTO xgb_dashopnvpn."EmailBroadcastTemplates"
  ("Name", "Description", "Subject", "BodyHtml", "CreatedByUserId", "CreateDate", "LastUpdate")
VALUES (
  'system.free_tier_channel_subscribe_reminder',
  $mightml$Built-in free-tier remind: channel subscribe + Telegram account link deep link. Placeholders: DISPLAY_NAME, REQUIRED_CHANNEL, CHANNEL_URL, LINK_CODE, LINK_TTL_MINUTES, CODE_LABEL, CODE_VALUE, ACTION_URL, ACTION_LABEL; optional BEGIN_LINK_ACCOUNT block.$mightml$,
  $mightml$Subscribe to our Telegram channel and link your account — DataGate$mightml$,
  $mightml$<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <meta name="color-scheme" content="light dark" />
  <meta name="supported-color-schemes" content="light dark" />
  <title>DataGate — subscribe and link Telegram</title>
  <style type="text/css">
    html, body {
      margin: 0 !important;
      padding: 0 !important;
      width: 100% !important;
      -webkit-text-size-adjust: 100%;
      -ms-text-size-adjust: 100%;
    }

    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif;
      font-size: 16px;
      line-height: 1.55;
      color: #1f2328;
      background-color: #f6f8fa;
    }

    .wrap { width: 100%; background-color: #f6f8fa; }
    .shell { max-width: 600px; margin: 0 auto; }

    .card {
      background-color: #ffffff;
      border: 1px solid #d0d7de;
      border-radius: 12px;
      overflow: hidden;
    }

    .accent {
      height: 4px;
      line-height: 0;
      font-size: 0;
      background: linear-gradient(90deg, #238636, #2ea043);
    }

    .pad { padding: 28px 24px 24px; }

    .email-title {
      margin: 0 0 4px;
      font-size: 22px;
      font-weight: 600;
      color: #1f2328;
      letter-spacing: -0.02em;
    }

    .email-tagline {
      margin: 0 0 24px;
      font-size: 13px;
      color: #656d76;
    }

    .email-lead {
      margin: 0 0 16px;
      font-size: 16px;
      font-weight: 500;
      color: #24292f;
    }

    .email-body {
      margin: 0 0 16px;
      font-size: 15px;
      color: #424a53;
      line-height: 1.55;
    }

    .email-body:last-of-type { margin-bottom: 0; }

    .email-body a {
      color: #0969da;
      text-decoration: none;
      font-weight: 500;
    }

    .email-body a:hover { text-decoration: underline; }

    .email-signoff {
      margin: 0;
      font-size: 15px;
      color: #424a53;
      line-height: 1.55;
    }

    .btn-wrap { margin-top: 16px; margin-bottom: 8px; }

    .btn {
      display: inline-block;
      padding: 10px 18px;
      font-size: 14px;
      font-weight: 600;
      text-decoration: none;
      border-radius: 8px;
      background-color: #238636;
      color: #ffffff !important;
      border: 1px solid #2ea043;
    }

    .footer {
      padding: 20px 24px 28px;
      font-size: 12px;
      line-height: 1.5;
      color: #656d76;
      text-align: center;
      border-top: 1px solid #d0d7de;
      background-color: #f6f8fa;
    }

    .footer a { color: #0969da; text-decoration: none; font-weight: 500; }

    .footer-muted { margin-bottom: 8px; }

    @media (prefers-color-scheme: dark) {
      body {
        color: #e6edf3 !important;
        background-color: #0d1117 !important;
      }
      .wrap { background-color: #0d1117 !important; }
      .card {
        background-color: #161b22 !important;
        border-color: #30363d !important;
      }
      .email-title { color: #f0f6fc !important; }
      .email-tagline { color: #9da7b3 !important; }
      .email-lead { color: #e6edf3 !important; }
      .email-body,
      .email-signoff {
        color: #c9d1d9 !important;
      }
      .email-body a { color: #79c0ff !important; }
      .code-panel {
        background-color: #21262d !important;
        border-color: #30363d !important;
      }
      .code-panel div:last-child { color: #f0f6fc !important; }
      .footer {
        color: #9da7b3 !important;
        border-top-color: #30363d !important;
        background-color: #0d1117 !important;
      }
      .footer a { color: #79c0ff !important; }
    }

    @media only screen and (max-width: 620px) {
      .pad { padding: 22px 18px 18px !important; }
      .email-title { font-size: 20px !important; }
      .btn { display: block !important; text-align: center !important; }
    }
  </style>
</head><body>
<table role="presentation" class="wrap" width="100%" cellpadding="0" cellspacing="0" border="0">
  <tr>
    <td align="center" style="padding:24px 12px;">
      <table role="presentation" class="shell" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;">
        <tr>
          <td>
            <table role="presentation" class="card" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#ffffff;border:1px solid #d0d7de;border-radius:12px;">
              <tr>
                <td class="accent">&nbsp;</td>
              </tr>
              <tr>
                <td class="pad">                    <p class="email-title">Subscribe to our channel and link Telegram</p>
                    <p class="email-tagline">Free/Default VPN needs a Telegram channel subscription or a linked Google/password ↔ Telegram account.</p>
                    <p class="email-lead">Hello {{DISPLAY_NAME}},</p>
                    <p class="email-body">To keep using Free/Default VPN access, please subscribe to our official Telegram channel and link your Google (or password) account with Telegram.</p>
                    <p class="email-body">Channel: <strong>{{REQUIRED_CHANNEL}}</strong><br/><a href="{{CHANNEL_URL}}" target="_blank" rel="noopener noreferrer">{{CHANNEL_URL}}</a></p>
                    <p class="email-body"><!--BEGIN_LINK_ACCOUNT-->Open the button below — Telegram opens our bot and applies link code <strong>{{LINK_CODE}}</strong> automatically (valid <strong>{{LINK_TTL_MINUTES}}</strong> min). Or send <code>/link_account {{LINK_CODE}}</code> to the bot.<!--END_LINK_ACCOUNT--></p>
                    <p class="email-body">If you need help: <a href="https://t.me/KolganovIvan" target="_blank" rel="noopener noreferrer"><b>@KolganovIvan</b></a></p>
            <div class="btn-wrap">
              <a class="btn" href="{{ACTION_URL}}" target="_blank" rel="noopener noreferrer">
                {{ACTION_LABEL}}
              </a>
            </div>

            <br />
                <div class="code-panel" style="margin:18px 0 12px;padding:14px 16px;border:1px solid #d0d7de;border-radius:10px;text-align:center;background:#f6f8fa;">
                  <div style="font-size:12px;color:#656d76;text-transform:uppercase;letter-spacing:0.06em;margin-bottom:8px;">{{CODE_LABEL}}</div>
                  <div style="font-size:26px;font-weight:700;letter-spacing:0.18em;font-family:ui-monospace,SFMono-Regular,Menlo,Monaco,Consolas,monospace;color:#24292f;">{{CODE_VALUE}}</div>
                </div>
                    <p class="email-signoff">— The DataGate team</p>
                </td>
              </tr>
              <tr>
                <td class="footer">
                  <div class="footer-muted">
                    Open clients and server-side tools for full control of your VPN
                  </div>
                  <div class="footer-muted">
                    <a href="https://datagateapp.com/" target="_blank" rel="noopener noreferrer">datagateapp.com</a>
                  </div>
                  <div>© 2026 DataGate v.1.0.3</div>
                </td>
              </tr>
            </table>
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>
</body>
</html>$mightml$,
  NULL,
  NOW() AT TIME ZONE 'utc',
  NOW() AT TIME ZONE 'utc'
)
ON CONFLICT ("Name") DO UPDATE SET
  "Description" = EXCLUDED."Description",
  "Subject" = EXCLUDED."Subject",
  "BodyHtml" = EXCLUDED."BodyHtml",
  "LastUpdate" = NOW() AT TIME ZONE 'utc';

-- verify
SELECT "Name", "Subject",
       POSITION('BEGIN_LINK_ACCOUNT' IN "BodyHtml") > 0 AS has_link_block,
       POSITION('{{ACTION_URL}}' IN "BodyHtml") > 0 AS has_action_url,
       "LastUpdate"
FROM xgb_dashopnvpn."EmailBroadcastTemplates"
WHERE "Name" = 'system.free_tier_channel_subscribe_reminder';
