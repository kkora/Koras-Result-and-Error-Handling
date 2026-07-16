# Data Protection — Koras.Results

## What the library does and does not handle

Koras.Results holds **no data at rest and moves no data in transit**. It performs no file,
database, or network I/O; it has no storage, no caching, and no telemetry export of its own. The
only "data" the library touches is what the application itself places into `Error` objects
(`Message`, `Metadata`, `FieldError` contents) and `Result<T>` values — in memory, for the
lifetime of the value.

Consequences:

- Encryption at rest / in transit are entirely the application's concern. When you serialize a
  `Result<T>` onto a queue or into a cache (ADR-0007 wire shape), *that* transport and store must
  be secured by you; the library only defines the JSON shape.
- There is no data-retention behavior to configure: errors live as long as your references to
  them.
- GDPR/PII obligations attach to what *you* put into errors, which is why the rules below exist.

## PII rules for Message and Metadata

`Error.Message`, `Error.Metadata`, and `FieldError.Message` can end up in three places: HTTP
responses (per the exposure rules in [secure-configuration.md](secure-configuration.md)),
server logs, and serialized payloads crossing service boundaries. Treat all three as
long-lived and broadly visible. Therefore:

1. **No PII in `Error.Code`** — ever. Codes are stable identifiers (`"User.NotFound"`) that flow
   into responses (`errorCode` extension), traces (`koras.error.code` activity tag), and logs
   unredacted, by design. A code like `"User.jane.doe@example.com.NotFound"` would leak through
   every channel at once.
2. **Avoid PII in `Message`.** Prefer "The user does not exist." over "User jane.doe@example.com
   does not exist." If a message must reference the subject, reference the caller's own input
   nominally ("the specified email address") or an opaque id.
3. **Metadata may carry identifiers only when necessary**, and then only opaque internal ids —
   never names, emails, addresses, government ids, or free-text user content. Remember the wire
   shape serializes metadata, and `MetadataExposurePolicy.All` (if enabled) sends it to HTTP
   clients.
4. **`FieldError.PropertyName`** is a schema name ("Email"), not a value — never embed the
   rejected *value* in validation messages ("'not-an-email' is invalid" leaks user input into
   logs and responses).
5. **Exception content**: the `Result.Try` default mapper already excludes exception messages
   (only the exception type name is recorded). Custom mappers must uphold the same rule —
   exception messages routinely contain paths, hostnames, and user data.

## Logging redaction guidance

The core never logs. The AspNetCore package logs exactly two events under category
`Koras.Results.AspNetCore.ResultHttpMapper` (see `docs/troubleshooting/logging.md`):

- Event 1 (Debug): error **code**, **type**, and status code — safe fields only.
- Event 2 (Warning): emitted when `Unexpected` details are suppressed from a response; it
  includes the error code **and the original message**. This is deliberate — the message was
  withheld from the client precisely so operators can still see it — but it means *your log
  pipeline is where that message ends up*. Apply your standard log-store protections
  (access control, retention limits) accordingly. If your `Unexpected` messages may contain
  regulated data, add redaction at the logging-provider level for this category, or keep such
  data out of the messages at the source.

For application-level logging via `TapError`, follow the log-safety rule from
`docs/architecture/observability.md`: log `Code` and `Type` freely; treat `Message` and
`Metadata` as potentially sensitive at Information level and above.

Activity tags (`Koras.Results.OpenTelemetry`) export only `error.type`, `koras.error.code`, and
an aggregate count — no messages, no metadata — so trace backends receive no free-text failure
content from this library.

## Localization does not bypass suppression

Verified against `src/Koras.Results.AspNetCore/ProblemDetailsBuilder.cs` (`Build`): when an error
is `ErrorType.Unexpected` and `IncludeUnexpectedErrorDetails` is `false`, the response `detail`
is assigned the fixed constant `"An unexpected error occurred."` **and the
`IErrorMessageLocalizer` is not invoked at all for that detail** — the suppressed branch and the
localization branch are mutually exclusive (`if/else`):

```csharp
if (error.Type == ErrorType.Unexpected && !options.IncludeUnexpectedErrorDetails)
{
    problemDetails.Detail = SuppressedUnexpectedDetail;   // constant; localizer never sees the error
    ...log warning...
}
else
{
    problemDetails.Detail = localizer.Localize(error, culture);
}
```

Consequences:

- A custom localizer cannot leak a suppressed message, because it never receives control on the
  suppressed path. Suppression is applied *before* (in place of) localization output.
- The trade-off is that the generic suppression text itself is currently **not localized** — it
  is always the English constant. If localized generic text matters to you, that is a feature
  request, not a security override.
- For all non-suppressed messages and for validation field messages, the localizer *does* receive
  the full `Error`/`FieldError`. A localizer is therefore part of your trusted output path: it
  must not append sensitive context to messages it returns.

Integration coverage: `Unexpected_error_details_are_suppressed_by_default_and_logged` (no leak,
warning logged) and `Custom_localizer_translates_messages_and_field_messages` (localizer applied
on the normal path) in
`tests/Koras.Results.IntegrationTests/AspNetCore/MinimalApiIntegrationTests.cs`.

## Summary

| Channel | What can appear there | Your control |
|---|---|---|
| HTTP response | Code always; message unless `Unexpected` (suppressed by default); metadata only under `All` | Options + message/metadata hygiene |
| Server logs | Code, type, status (Debug); code + original message on suppression (Warning) | Log-pipeline access control / category filters |
| Traces | Code, type, aggregate count | None needed — no free text |
| Serialized results | Everything (code, message, type, metadata, field errors) | Use only on trusted internal transports |
