# Repository preparation evidence

These are new checks run during standalone repository preparation on 2026-10-07. They are separate from the original release's acceptance matrix. Full commands, interpretation and limitations are in [RELEASE_MANIFEST.md](../../RELEASE_MANIFEST.md).

Linux source build and 122 legacy/unified checks passed with the checksum-verified pinned SDK, using serialized MSBuild. Standard parallel restore failed in this container; cause not established. Ten original-binary Linux replay cases passed (20 invocations, 370 rows), six pinned ML methodology tests passed, and schema/publication checks are reported separately.

The existing live loopback integration harness **FAILED** its IPv4 ownership assertion. A separate probe confirmed sibling-process namespace/fd access is denied in this environment. This failure was not suppressed or relabeled PASS. No live exports, identities, spool or raw build logs are included. Full native packaging, Windows, fresh cross-platform parity and hosted Actions remain UNVERIFIED.

The preserved original release counts remain unchanged. The synthetic model/fixtures do not establish production accuracy. The scan reports document pattern scope, exact reviewed fake-value/source-expression exceptions and limitations; they are not an assurance that every possible secret type is covered.
