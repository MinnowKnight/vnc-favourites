# Changelog

## 1.3.0 - 2026-11-23
- Security: the VNC password is no longer passed on the viewer's command line, where other
  programs, Task Manager and process-auditing tools could read it. It now goes to the viewer in a
  temporary options file in your own Temp folder, which is wiped (overwritten, then deleted) as soon
  as the viewer has started, and also when VNC Favourites closes.
- Version number shown in the title bar.
- Source can be built on any Windows PC with `build.cmd` (no Visual Studio needed).

## 1.2.0
- Fixed: connections appeared zoomed in. TightVNC ignores its saved per-server display settings
  when started from the command line; VNC Favourites now passes those settings on.
- New per-favourite Scaling option (Same as TightVNC, Fit to window, 50%-200%).
- Status line shows which settings were used for each connection.

## 1.1.0
- Favourites shown as icon buttons; click to connect, right-click to edit, move or delete.
- Choice of 50 icons when saving a favourite.

## 1.0.0
- First version: favourites list, DPAPI-encrypted saved passwords, launches TightVNC Viewer.
