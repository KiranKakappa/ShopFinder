# ShopFinder

Find out which shops in your city are **open right now**, see what they sell, and grab **discounts** — and let shop owners update their status and post deals from their phone.

- **Backend:** ASP.NET Core 8 (C#) minimal API, Entity Framework Core + SQLite, JWT login
- **Frontend:** React 18 + Vite, Leaflet / OpenStreetMap maps (no API key needed)

## Run it (fastest way)

Requirements: **.NET 8 SDK** (https://dotnet.microsoft.com/download).

```bash
cd ShopFinder.Api
dotnet run
```

Open **http://localhost:5080**. The React app is already built into `ShopFinder.Api/wwwroot`, so you don't need Node for this.

On first run the app creates `shopfinder.db` and adds 8 demo shops. Every demo shop logs in at **/owner** with its phone number (e.g. `5550101`) and password `demo1234`.

API docs (Swagger) in development: **http://localhost:5080/swagger**

## Work on the frontend

Requirements: Node.js 18+.

```bash
# terminal 1
cd ShopFinder.Api && dotnet run

# terminal 2
cd shopfinder-web
npm install
npm run dev          # http://localhost:5173 (calls the API on :5080)
```

`npm run build` rebuilds the site into `ShopFinder.Api/wwwroot`.

## What it does

**Customers** (`/`)
- Map + list of shops, green = open, grey = closed. Refreshes every minute.
- "Open now" filter, category chips, search across shop names *and* products ("milk", "charger").
- "Near me" sorts by distance using the browser's location.
- **Deals** tab: every active discount in the city, open shops first.
- Shop panel: status, hours, products with old/new prices, directions, call, and **"Report closed"** if the app said open but the shop wasn't.

**Shop owners** (`/owner`)
- Sign up with phone + password, pin the shop on a map, set hours and open days.
- **Swipe switch**: swipe right to open, left to close (tap or arrow keys also work). A manual choice lasts only until midnight; then regular hours take over again, so a forgotten switch never misleads customers for days.
- Overnight hours (e.g. 6 PM → 3 AM) and 24-hour shops are supported.
- Add / edit / delete products, set a discount % with an optional end time, mark out of stock.
- See how many customers reported the shop closed today (cleared when the owner confirms a status).

## How "open" is decided

1. If the owner pressed Open/Closed **today**, that wins.
2. Otherwise the regular hours and open days decide (in the city's time zone).

## Configuration (`ShopFinder.Api/appsettings.json`)

| Setting | What it is |
|---|---|
| `App:TimeZone` | City time zone, IANA id like `America/New_York` or `Asia/Kolkata`. Empty = server's zone. **Set this when deploying** — servers usually run in UTC. |
| `App:SeedDemoData` | `false` to start with an empty city. |
| `App:SeedCenterLatitude/Longitude` | Where demo shops are placed. Change to your city, then delete `shopfinder.db` and restart. |
| `Jwt:Key` | Token signing secret. **Must** be changed for production (the app refuses to start in Production with the sample key). Use env var `Jwt__Key`. |
| `ConnectionStrings:Default` | SQLite file path. |

## API overview

| Method | Path | Who |
|---|---|---|
| GET | `/api/shops?q=&category=&openNow=true&lat=&lng=` | anyone |
| GET | `/api/shops/{id}` | anyone |
| POST | `/api/shops/{id}/report-closed` | anyone (rate-limited) |
| GET | `/api/deals?q=&category=&openNow=&lat=&lng=` | anyone |
| GET | `/api/categories` | anyone |
| POST | `/api/owner/register`, `/api/owner/login` | owner (rate-limited) |
| GET | `/api/owner/me` | owner (Bearer token) |
| PUT | `/api/owner/shop` | owner |
| POST | `/api/owner/status` `{ "mode": "open" \| "closed" \| "auto" }` | owner |
| POST/PUT/DELETE | `/api/owner/products[/{id}]` | owner |

Prices are in USD.

## Going to production — next steps

- Switch SQLite → PostgreSQL or SQL Server (swap the EF Core provider; add migrations with `dotnet ef migrations add Initial`).
- Phone verification by SMS (OTP) at sign-up, and an admin page to approve new shops.
- Product photos (blob storage), push notifications ("your favourite shop just opened"), reservations / pickup orders, payments.
- Wrap the React app as a mobile app with Capacitor, or build React Native screens on the same API.
