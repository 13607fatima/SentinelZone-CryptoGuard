# Debian packaging

Canonical debhelper inputs remain in `/debian/` to preserve the existing packaging. Build with `dpkg-buildpackage -b -us -uc`. Debian version `0.22.2~rc.1` maps to application release `0.22.2-rc.1`; the tilde ensures the release candidate sorts before stable `0.22.2`.
