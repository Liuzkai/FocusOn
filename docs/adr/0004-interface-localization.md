# ADR 0004: English and Simplified Chinese interface

Date: 2026-10-12. Status: accepted (explicit user request).

The desktop UI supports English and Simplified Chinese. A selector in Settings changes presentation immediately (moved from the first row by the 2026-10-14 request and ADR 0008). First launch follows a Chinese system UI language; other system languages fall back to English. The preference is saved per data directory in `settings.json`, independently of the recording database.

The core keeps its established record/status contracts and SQLite format. A shared presentation catalog translates known labels and complete diagnostic messages at the UI boundary, including cached warnings. User app names, window titles, timer titles, keys, UTC boundaries and duration ticks remain unchanged. Switching language does not restart observers or timers, or reset the selected day, record, filters or zoom.

The tray, confirmation dialogs, accessibility labels, timeline lanes, interval labels and duration formatting use the selected language. If saving a preference fails, the previous selection is retained and the failure is shown. Invalid preference files fall back to the system language when read; they are not silently overwritten.
