> TARİXİ BASELINE SƏNƏDİ: Bu fayl Linux 0.22.1-dən saxlanıb. 0.22.2-rc.1 üçün README.md, docs/TELEMETRY-CONTRACT.md, docs/ACCEPTANCE.md və release validation/ nəticələri əsasdır.

# Version-sensitive references checked 2026-10-04

Original implementation; references below describe APIs and packaging, not copied
code dependencies. Historical research-repository licenses are not relied upon.

- .NET Native AOT/Linux baseline: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/
- Native AOT cross-OS restrictions: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/cross-compile
- Ubuntu/.NET 10 support: https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install
- Official SDK release metadata/SHA-512: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
- JSON Schema exporter: https://learn.microsoft.com/en-us/dotnet/api/system.text.json.schema.jsonschemaexporter?view=net-10.0
- hwmon units and labels: https://docs.kernel.org/hwmon/sysfs-interface.html
- NVIDIA NVML API: https://docs.nvidia.com/deploy/nvml-api/latest/
- DRM client usage: https://docs.kernel.org/gpu/drm-usage-stats.html
- AMD GPU busy sysfs: https://www.kernel.org/doc/html/latest/gpu/amdgpu/thermal.html
- Debian policy: https://www.debian.org/doc/debian-policy/
- dh_installsystemd: https://manpages.debian.org/trixie/debhelper/dh_installsystemd.1.en.html
- dpkg-shlibdeps: https://manpages.debian.org/bookworm/dpkg-dev/dpkg-shlibdeps.1.en.html
- systemd notify protocol: https://github.com/systemd/systemd/blob/main/src/systemd/sd-daemon.h
- .NET 10.0.12 license: https://github.com/dotnet/runtime/blob/v10.0.12/LICENSE.TXT
- .NET third-party notices: https://github.com/dotnet/runtime/blob/v10.0.12/THIRD-PARTY-NOTICES.TXT
- GitHub workflow security: https://docs.github.com/en/actions/reference/security/secure-use

Ubuntu 22.04 build base digest:
sha256:08ea48a03a3e78ebc7cd526e6a275053223aadd88bfc09cc49b06d5281525fde.
SDK archive SHA-512 is validated in packaging/Containerfile.ubuntu22.
Initial managed tests used Ubuntu SDK 10.0.112; final Native AOT uses the verified
Microsoft SDK 10.0.401 and its matching runtime-pack tooling.
