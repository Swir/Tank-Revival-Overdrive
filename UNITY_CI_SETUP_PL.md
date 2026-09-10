# Jednorazowa konfiguracja buildów Windows na GitHub

Gracz **nie potrzebuje Unity**. Po poprawnym buildzie pobierasz z GitHub Releases ZIP, rozpakowujesz go i uruchamiasz `TankRevivalOverdrive.exe`.

Unity jest potrzebne wyłącznie komputerowi CI w GitHub Actions. Unity wymaga aktywnej licencji także przy kompilacji automatycznej.

## Unity Personal — najprostsza konfiguracja

1. Załóż / użyj konta Unity.
2. Zainstaluj **Unity Hub** na Windows. Pełnego edytora Unity nie musisz instalować do grania; Hub jest potrzebny do jednorazowej aktywacji darmowej licencji Personal.
3. W Unity Hub otwórz `Preferences > Licenses > Add` i wybierz darmową licencję Personal.
4. Otwórz plik:
   `C:\ProgramData\Unity\Unity_lic.ulf`
5. W repozytorium GitHub przejdź do:
   `Settings > Secrets and variables > Actions > New repository secret`
6. Dodaj sekrety:
   - `UNITY_LICENSE` — pełna zawartość pliku `Unity_lic.ulf`
   - `UNITY_EMAIL` — e-mail konta Unity
   - `UNITY_PASSWORD` — hasło konta Unity

**Nie wysyłaj tych danych w issue, commicie ani na czacie.** Umieszczaj je tylko w GitHub Secrets. Dobrą praktyką jest używanie osobnego konta Unity przeznaczonego do CI.

## Budowanie Release

1. Otwórz zakładkę `Actions` w repo.
2. Wybierz `Build Windows Release`.
3. Kliknij `Run workflow`.
4. Wpisz tag, np. `v0.1.0`.
5. Po udanym buildzie workflow automatycznie utworzy / zaktualizuje GitHub Release.
6. W `Releases` pobierz `TankRevivalOverdrive-Windows-x64.zip`.
7. Rozpakuj cały ZIP do jednego folderu.
8. Uruchom `TankRevivalOverdrive.exe`.

Nie przenoś samego EXE bez folderu `TankRevivalOverdrive_Data` — oba elementy są częścią gry Unity.

## Co sprawdza pipeline przed publikacją

Workflow publikuje Release dopiero, gdy istnieją:

- `TankRevivalOverdrive.exe`
- `TankRevivalOverdrive_Data/`
- niepusty ZIP z kompletnym buildem Windows x64

Dzięki temu błąd kompilacji nie powinien trafić do Releases jako pozornie gotowa gra.
