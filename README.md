# SlothApp

Simple WPF utility to preview and move documents into a destination folder structure using a JSON configuration and a customer spreadsheet.

## What it does (short)
- Loads a JSON configuration that defines document sets, matching and destination rules.
- Loads customers from an Excel/CSV file.
- Scans a source folder for files and, based on selected customer and doc code, previews or moves each file to a destination path determined by the configuration and naming rules.
- Optionally writes an Excel log of moved files.

## Key features
- Preview mode (no filesystem changes) and Run mode (performs moves).
- Config-driven document rules and destination naming formats.
- Customer-driven choices (category → allowed doc codes).
- Persistent UI paths via `UserSettings`.
- Simple grid view of results (`MoveResult`) with success/failure messages.
- Sample input files in `SlothTest` (config + customers).

## Quick start (developer)
1. Open the solution in Visual Studio 2022.
2. Set `SlothApp` as the startup project.
3. __Build Solution__ and __Start Debugging__ (F5) or run the built exe.
4. Use the UI:
   - Browse and `Load Config` (JSON)
   - Browse and `Load Customers` (XLSX / CSV)
   - Select a customer and a Doc Code
   - Set `Source` folder and `Dest Root`
   - Click `Preview` to see planned moves, or `Run` to execute and optionally write the log

## Where to find samples
- `SlothTest\SlothConfig.json` — sample configuration (document sets, matching, destination settings).
- `SlothTest\Customers.csv` — sample customers used by the UI.

## Important types (what to read next)
- `SlothApp\MainWindow.xaml.cs` — UI glue: dialogs, validation, launching the batch flow.
- `Sloth.Core\Models\SlothConfig` — configuration model (document sets, matching, dest settings).
- `Sloth.Core\Models\Customer` — customer data used to map to document sets.
- `Sloth.Core\Models\SourceDoc` — lightweight input for each source file.
- `Sloth.Core\Models\MoveResult` — row returned for each file (dest path, success, message).
- `Sloth.Core\Services\ConfigService` — load/parse JSON config.
- `Sloth.Core\Services\ExcelService` (+ extensions) — read customers; write move log.
- `Sloth.Core\Services\BatchMoveService` — core preview/execute logic.
- `Sloth.Core.Services\MatchingService`, `NamingService`, `RoutingService` — rules for matching, naming, and destination path creation.
- `Sloth.Core\Services\UserSettings` — persists last-used UI paths.

## Typical workflow (user-facing)
1. Load config and customers.
2. Select customer → UI limits doc codes to those in the config for the customer's category.
3. Choose a doc code.
4. Ensure `Source` folder contains files to route.
5. Preview to verify mapping. If OK, Run to move files.
6. If configured, a move log (Excel) will be written on Run.

## Notes & gotchas
- The app requires real source and destination directories; it will raise user-facing errors if missing.
- Document sets are keyed by `Customer.Category` — customers without a category mapping will show no doc codes.
- Log writing is attempted only after a successful `Run`.
- UI saves paths automatically when the window closes.

## Contributing / Extending
- To change matching, naming or routing behavior, inspect and modify:
  - `BatchMoveService`, `MatchingService`, `NamingService`, `RoutingService`.
- Add unit tests around `MatchingService` and `NamingService` to validate rule changes.

## Where to run tests / debugging tips
- Put breakpoints in `RunCore` (in `MainWindow.xaml.cs`) and step into `BatchMoveService.PreviewAndExecute` while running a small sample set.
- Use the `SlothTest` sample files to simulate real inputs.

---

Created as a concise summary to help new contributors and reviewers quickly understand project responsibilities and where to look next.
