# Documents and capabilities API

Project documents: drawings, permits, contracts, insurance policies, certificates, photos and reports. Each document has one or more
**versions**, each either a stored **file** or an `https://` **link** to where it is kept. A new version never replaces the older ones, which
stay downloadable. Documents are **voided with a reason, never deleted**. All routes are under `/api`, return JSON in camelCase and send
`Cache-Control: private, no-store`. Dates are `"2026-11-02"`; instants are UTC. Writes need the anti-forgery header, like every other write.

The capability routes report what the system depends on that may not be set up (file storage, virus scanning, email).

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/projects/{projectId}/documents?category&page&pageSize` | `documents.read` |
| `GET /api/documents/{id}` | `documents.read` |
| `GET /api/documents/{id}/versions/{number}/file` | `documents.read` |
| `POST /api/projects/{projectId}/documents` (JSON for a link, multipart for a file) | `documents.manage` |
| `POST /api/documents/{id}/versions` (JSON for a link, multipart for a file) | `documents.manage` |
| `PUT /api/documents/{id}` | `documents.manage` |
| `POST /api/documents/{id}/void` | `documents.manage` |
| `GET /api/capabilities` | `settings.manage` |

Roles: Administrator, Project Manager, Cost Engineer and Accountant manage (`documents.manage`); they and the Warehouse Keeper and the Viewer
read (`documents.read`). The HR Officer has no access. **Contract and Insurance documents also need `costs.read`** (they reveal prices), so a
Warehouse Keeper never sees them: they are left out of lists, a `category=Contract` filter answers an empty list, and their id, their
versions and their files are `404`, the same as an id that does not exist. A document, version or project of another company is `404`. A
change on an archived project is refused (restore the project first); its documents stay readable and downloadable. A stale `version` is
`409`.

## The document

`category` is `Contract`, `Drawing`, `Permit`, `Insurance`, `Certificate`, `Photo`, `Report` or `Other`. `title` is up to 120 characters,
`description` up to 300. `validUntil` is allowed only for `Permit`, `Insurance` and `Certificate`. `status` is `Active` or `Void`. For an
Active document with a `validUntil`, `expiry` is `Valid`, `ExpiringSoon` (30 days or fewer left; the last valid day counts) or `Expired`,
and `daysLeft` counts from today in the company's time zone (negative once past). Both are worked out when read and never stored (the bond
rule); otherwise both are `null`.

A version has `number` (1, 2, ...), `kind` (`File` or `Link`), and for a file `fileName` (cleaned, up to 200), `contentType` (decided by the
server from the type, never from what the browser said), `sizeBytes` and `sha256` (lower-case hex, worked out while the file was stored);
for a link `url`. Both have an optional `note` (up to 300), `addedByName` and `addedAtUtc`. **Where a file is stored is never sent.**

`GET /api/projects/{projectId}/documents` returns `{ items, total, page, pageSize, uploads }`. Items are Active documents first (by
category, then title), then the voided ones, each with its newest version as `current`. `uploads` says whether a file can be uploaded now:
`{ available, state, message, maximumBytes, allowedExtensions }`; when `available` is false, `message` says why in plain words and that a link
still works. `GET /api/documents/{id}` adds `projectName`, `projectArchived`, `voidReason`, `voidedByName`, `voidedAtUtc`, `createdByName`,
`createdAtUtc`, `currentVersionNumber` and `versions` (newest first, at most the latest 100).

## Writes

* **A new document with a link** (JSON): `title`, `description`, `category`, `validUntil`, `url`, `note`, and an optional `clientRequestId`
  (a repeated request id returns the first document; the same id on another project is refused). Answers `201` with the document.
* **A new document with a file** (multipart form data): the text fields `title`, `description`, `category`, `validUntil`, `note`,
  `clientRequestId` **first**, then the file part named `file` **last**. A field sent after the file is ignored. Answers `201`.
* **A new version** (`POST /api/documents/{id}/versions`): JSON `{ url, note, validUntil, version }` for a link, or multipart with the fields
  `note`, `validUntil`, `version` and then `file`. `validUntil` is optional: a renewed permit, policy or certificate can carry its new date,
  saved with the version in one change; empty keeps the date. A date on a kind that has none is refused.
* `PUT /api/documents/{id}` changes `title`, `description`, `category` and `validUntil` (with `version`). Saving unchanged details records
  nothing. A person without `costs.read` cannot move a document into Contract or Insurance.
* `POST /api/documents/{id}/void` takes `reason` (required, up to 300) and `version`. A voided document takes no new version and no change;
  its versions stay downloadable.

Problems with what was typed come back together as `422` field errors (`title`, `category`, `validUntil`, `url`, `note`, `file`). Audit
lines carry the title and the category, never the file name, the link or the note.

### Files

* **Allowed types**: pdf, jpg, jpeg, png, webp, doc, docx, xls, xlsx, ppt, pptx, csv, txt, dwg, dxf. Everything else (html, svg, js, exe, a
  name without a type) is refused before anything is stored. A deployment can narrow the list (`FileStorage:AllowedExtensions`), never
  widen it.
* **Content check**: a pdf must start with `%PDF-`, a jpg/jpeg with `FF D8 FF`, a png with its 8-byte signature, a webp with `RIFF....WEBP`.
  A file that does not is removed and refused ("This file isn't a real PDF"). An empty file is refused.
* **Size**: 25 MB by default (`FileStorage:MaxFileMegabytes`). The limit is enforced while the file streams in; a larger file is refused with
  no file and no row left behind. The upload routes accept a request up to the limit plus 1 MB.
* **Storage**: a file is kept under `<company id>/<project id>/<random name>` in the configured folder. The name a person chose never
  reaches a path; it is cleaned (folders, control and reserved characters and right-to-left marks removed, at most 200 characters) and used
  only as the download name. A save that fails partway leaves no file and no row.
* **Downloads** (`GET .../versions/{number}/file`) stream the file with `Content-Disposition: attachment` and the cleaned name,
  `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-store` and the site-wide `Content-Security-Policy: default-src 'none'`. A
  link version has no file (`404`). A file missing from storage is `404` with a plain message to restore it from the backup.
* **Not scanned**: files are not scanned for viruses. They are offered only as downloads, never shown in the browser.

### When file storage is not ready

Uploading needs the File storage capability to be `Available`. Otherwise an upload is refused with `409` and the plain message from the
capability (for example "File storage is not set up yet, so files can't be uploaded. ... Add a link to the document instead."), before any
byte is stored. **A link always works**, so no one's work is lost.

## Capabilities

`GET /api/capabilities` (administrators) returns `{ items: [{ key, name, state, message, fallback }] }` in a fixed order. `state` is one of:

| State | Meaning |
| --- | --- |
| `Available` | Set up and working. `fallback` is null. |
| `NeedsSetup` | Not set up yet; someone has to configure it. The fallback works meanwhile. |
| `Unavailable` | Set up but switched off on purpose. |
| `Failing` | Set up but not working right now (the last check failed). |

| Key | Name | States |
| --- | --- | --- |
| `file-storage` | File storage | `NeedsSetup` when `FileStorage:RootPath` is empty; `Unavailable` when `FileStorage:UploadsEnabled` is false (the emergency switch; stored files still download); `Failing` when a test file can't be written there; otherwise `Available`. The write test is cached for 30 seconds. |
| `virus-scanning` | Virus scanning | Always `NeedsSetup`: files are stored and offered only as downloads; they are not scanned. |
| `email-delivery` | Email delivery | Always `NeedsSetup`: notifications appear in the app; email is not set up. |

A message never contains a path, a vendor name or an error code. A capability with no registered check is reported as `NeedsSetup`, never
as available.

## Statement counts

The list is four statements however many documents and versions exist (the project, the company's day, the count and the page), a document
three, and a download one; `DocumentAccessApiTests` pins these.
