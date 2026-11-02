# Project documents and system readiness (slice F rules note, for the lead to merge into docs/business-rules.md)

## Access rows (section 2 table)

| Permission | Administrator | Project Manager | Cost Engineer | Accountant | Warehouse Keeper | HR Officer | Viewer |
| --- | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
| See project documents and download them (contracts and insurance policies only with "See budget and costs") | yes | yes | yes | yes | yes | | yes |
| Add, replace, change and void project documents | yes | yes | yes | yes | | | |
| See system readiness (file storage, virus scanning, email) | yes | | | | | | |

So a Warehouse Keeper sees drawings, permits, certificates, photos, reports and other documents, and never a contract or an insurance
policy (they reveal prices); a Viewer sees all of them but changes nothing; an HR Officer sees none.

## Needs your action row (section 16 table)

| Group | Who receives it (permission) | What it counts | Amount shown |
| --- | --- | --- | --- |
| Documents that have expired or expire soon | Manage documents: Administrator, Project Manager, Cost Engineer, Accountant | Active documents on projects that are not archived whose valid-until date has passed or is within 30 days (the bond rule), expired first, then the soonest | **None** |

The row reads "Expired 3 days ago, on 3 Dec 2026", "Expires today", "Expires tomorrow" or "Expires in 12 days, on 15 Dec 2026"; an expired
one is urgent and says "Expired". It opens the project's Documents tab. Contract and Insurance documents are left out for anyone who cannot
see costs (every role that manages documents can, today).

## Rules

* **What a document is.** A document belongs to one project and has a title (up to 120 characters), an optional description (up to 300), a
  kind (Contract, Drawing, Permit, Insurance, Certificate, Photo, Report or Other) and one or more versions. A version is either a stored
  file or an `https://` link (up to 500 characters; any other scheme, spaces, or a user name or password in the address is refused), with
  an optional note (up to 300), who added it and when.
* **Versions are kept.** Adding a version makes it the current one (numbered 1, 2, 3, ...); every older version stays and can still be
  downloaded or opened. A version is never changed or removed. Two people adding a version at the same moment cannot both make the same
  number: the second is asked to reload.
* **Valid-until date.** Only a permit, an insurance policy or a certificate has one (optional). It is judged on the company's calendar day,
  exactly like a bond: **expiring soon** when 30 days or fewer are left (the last valid day counts: "Expires today"), **expired** once the
  date has passed. The state is worked out when read and never stored, and a voided document has none. A new version of a dated document
  can carry the renewed date in the same change; leaving it empty keeps the old date.
* **Contracts and insurance reveal prices.** They are shown, downloaded, added or changed only by people who can see budget and costs. To
  anyone else they do not exist: not in lists, not by id, not by download, not in "Needs your action".
* **Voiding, never deleting.** A document added by mistake or no longer in use is voided with a reason (required, up to 300). It stays on
  the list under "Voided", with who and when, and its versions stay downloadable. A voided document takes no new version and no change.
* **Archived projects.** Their documents can be read and downloaded; nothing can be added or changed until the project is restored.
* **Idempotent add.** Adding a document carries a request id: sending the same form twice (a double tap, a retry after a dropped connection)
  returns the first document and keeps one file.
* **Concurrency.** Every change carries the version the person opened; a stale one is refused and the person reloads.
* **Audit.** Adding, a new version, changing details and voiding are recorded with the title and kind only, never the file name, the link,
  the note or the reason.

## Files

* **Allowed types**: PDF, JPG/JPEG, PNG, WebP, Word (doc, docx), Excel (xls, xlsx), PowerPoint (ppt, pptx), CSV, text and CAD drawings (dwg,
  dxf). Anything else (web pages, SVG, scripts, programs, a name without a type) is refused before anything is stored.
* **The server looks inside.** A PDF and the image types must really start like one; a file that does not ("This file isn't a real PDF")
  is removed and refused. The type the file is sent back as is decided by the server from its type, never from what the browser said.
* **Size**: up to 25 MB per file (configurable). A larger file is refused while it arrives, and nothing is kept.
* **Safe storage**: a file is kept under a name the server makes (the company, the project and a random name). The name a person chose
  is cleaned and used only as the download name; it never reaches the server's disk.
* **Safe downloads**: files are only ever offered as downloads (never opened in the browser), not cached, with the browser told not to
  guess the type.
* **Not scanned for viruses** (deliberately not built): the System readiness page says so.
* **When file storage is not ready** (not set up, switched off, or failing), uploading is turned off with the reason in plain words at the
  point of use, and "Add a link instead" still works, so nothing typed is lost. A failed upload leaves no file and no record.

## System readiness (Administration)

One check per capability decides what can be used now and says it in plain words, with what still works meanwhile:

| Capability | State | What people see |
| --- | --- | --- |
| File storage | Working when a storage folder is set and can be written; Not set up when none is set; Switched off when an administrator turned uploads off; Not working when the folder can't be written | Upload a file, or add a link instead |
| Virus scanning | Always Not set up | Files are stored and offered only as downloads; they are not scanned |
| Email delivery | Always Not set up | Notifications appear in the app; email is not set up |

## Defaults (not confirmed by the owner)

* **Default**: expiring soon at 30 days, the same figure as bonds.
* **Default**: 25 MB per file; the type list above; no virus scanning.
* **Default**: documents are grouped by kind on the project's Documents tab, with voided ones folded under "Voided".
* **Default**: a new version may carry a renewed valid-until date (one change instead of two).
* **Default**: an emergency switch (`FileStorage:UploadsEnabled`, on by default) stops new uploads without hiding stored files.
* **Default**: the System readiness page is for administrators only (`settings.manage`).
