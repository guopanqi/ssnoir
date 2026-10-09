# TapTap WeixinGameConverter v2.0.5 — provenance

The source file `wx_converter.py.gz` is a losslessly gzipped copy of the exact `wx_converter.py` supplied by the user in `taptap-converter.zip` (no bundled `node_modules`).

- Original converter SHA-256: `19020e1b26ce360d156da07676326885957a4b98a4624457e328518b97974354`.
- Original uploaded ZIP SHA-256: `4249895a68922afc32196a8ae5818966ff0a997ad58e866c1ff7915c22bb3b4e`.
- Metadata: `CONVERTER_VERSION = "2.0.5"`.
- The converter is executed directly by `scripts/build-taptap.mjs`; the source hash is verified before decompression and execution.
- `.babelrc`, `wx_unity_converter/wx_unity.js` and `check-version.js` are supplied vendor support files. The current no-plugin SSNoir build uses these; Unity-specific cached plugin payloads from the archive were intentionally not copied. If SSNoir adds WeChat plugins later, revisit this packaging choice.
- The checked-in support files are functionally equivalent to the supplied 2.0.5 counterparts. The converter Python source is byte-for-byte authentic.

**Do not** edit the compressed upstream source to fix game runtime behavior. Place SSNoir-specific platform adapters under `Game/src/platform/`, and treat converter upgrades as explicit dependency reviews.
