# CMDB

A web-based Configuration Management Database built with ASP.NET Core MVC and Oracle DB. The schema is discovered dynamically from Oracle — every user table becomes an entity automatically.

---

## Authentication

### `/login` — Sign In
The only public page. All other routes redirect here if no session exists.

- **Email field** — enter any email address
- **Send login link** button — generates a one-time token (expires in 15 min), logs the magic link to the server console. On localhost, a clickable **"Click here to sign in"** link is displayed directly on the page. Link is also shown on localhost in the Login Page.

### `/auth?token=...` — Magic Link Handler
Validates the token, creates a session, redirects to `/`.

### `/logout` — Sign Out
Clears the session, redirects to `/login`. Accessible via **Sign out** in the top navbar.

---

## Pages

### `/` — Dashboard
Lists all discovered Oracle tables as clickable cards. Each card shows the table's icon, display name, and description.

- **Search / create bar** — filters cards live as you type. If no exact match exists, a red **Create** button appears to create a new table with that name.

---

### `/entity/{table}` — Entity List
Paginated table view (10 rows/page) for any entity.

**Top bar:**
| Control | Description |
|---|---|
| Search field + **Search** | Full-text search across all text columns and FK display values |
| **Clear** | Clears the active search (shown only when a search is active) |
| **Create** (red) | Opens the Create form |

**Table:** Sortable columns (click header to toggle asc/desc). The first cell of each row is a blue **[id]** button that opens the Edit form.

**Bottom bar (right side):**
| Button | Description |
|---|---|
| **Schema** (green dropdown) | Opens the schema editor panel (see below) |
| **Export CSV** | Downloads all rows as a `.csv` file |
| **Import CSV** | File picker — uploads a `.csv`, inserts new rows or updates existing ones matched by `NAME` |
| **Sync** *(shown only when a sync source is available)* | Pulls data from the configured `SyncBaseUrl` API and upserts into the table |

**Schema dropdown** (green gear):
| Section | Controls |
|---|---|
| Add Column | Column name text field + type selector (Text / Number / Date / Timestamp) + **Add** |
| Drop Column | Column selector + **Drop** |
| Add Reference | Table selector (adds a `{TABLE}_ID` FK column) + **Add** |
| Drop Reference | FK column selector + **Drop** |
| — | **Drop Table** button (confirms before executing) |

---

### `/entity/{table}/create` — Create Record
Form with one input per non-PK column. FK columns render as autocomplete pickers. Identity PK columns are hidden (auto-assigned by Oracle).

- **Create** button — submits and redirects to the list
- **Back to List** link

---

### `/entity/{table}/edit` — Edit Record
Pre-populated form for an existing row. FK columns render as autocomplete pickers with the current value selected.

- **Save** (red) — submits changes, redirects to list
- **Delete** (outline red) — navigates to the Delete confirmation page
- **Back to List** link

---

### `/entity/{table}/delete` — Delete Confirmation
Shows all field values of the record before deletion.

- **Delete** button — permanently removes the record, redirects to list
- **Back to List** link

---

### `/entity/{table}/details` — Record Details
Read-only view of a single record with all fields displayed, FK columns resolved to their display values.

---

## Navbar (all authenticated pages)
| Link | Action |
|---|---|
| **CMDB** (brand) | Go to Dashboard `/` |
| **Home** | Go to Dashboard `/` |
| **Sign out** | Clears session → `/login` |

---

## API

### `GET /api/{table}` — Sync Source Endpoint
Returns a JSON array for the named table. Used internally by the Sync feature. This route is **public** (no auth required) so the server can call itself during sync.
