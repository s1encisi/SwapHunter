# SwapHunter original Three.js assets

Run `npm ci --ignore-scripts` then `npm run build` in this directory. Node.js and Three.js 0.186.1 are the only authoring dependencies. The generator creates all geometry from its own numeric descriptions; it does not read any external game's files or assets.

Output: `Game/Assets/_SwapHunter/Art/ThreeModels/*.glb`. Each GLB contains original mesh geometry, standard PBR materials, named articulation groups, sight/muzzle markers where relevant, and axis probes. `asset-manifest.json` records native Three.js mesh/triangle counts and meter-scale bounds.

Unity package: `com.unity.cloud.gltfast` 6.15.0. Run `SwapHunter.Editor.AssetImportAudit.Run` to import and check assets. The audit determines the importer axis correction from the embedded probes and creates `ModelLibrary.asset`. It records Unity-side geometry evidence under `Logs/V03/asset-import.json`.

The playable project instantiates this model library. Gameplay colliders are authored separately so decorative bevels, rails and fingers do not change navigation or swap validity. Keep the generated `.meta` files when regenerating GLBs, so Unity references remain stable.

Three.js is MIT-licensed; the original license is copied into the generated asset directory. Model geometry and layout are independently authored for SwapHunter, with AI assistance. No Counter-Strike source, sounds, models, textures, map files, or installed game directories are used.
