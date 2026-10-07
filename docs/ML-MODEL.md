# Random Forest — yalnız shadow

Chayesh README 22 feature deyir, lakin yoxlanılan `9e08ca73f2a9abf08d5fc3700f3fefce8552bf3d` commit-in `collect_data_linux.py` faylında metadata xaric 21 numerical feature var. Temperatur training zamanı çıxarılanda say yenə dəyişir. SentinelZone ayrıca versiyalanmış **22-feature** contract istifadə edir: `contracts/ml-features.json`. Upstream pickle, scaler və dataset daşınmayıb. Ətraflı müqayisə `CHAYESH-FEATURE-AUDIT.md`-dədir.

`research/training/CryptoGuard.DatasetCollector` yalnız redacted agent JSONL-dən dataset yaradır. host_id hash pseudonym-dir, session_id GUID-dir, hardware profile, workload_label, started_at, observed_at, feature contract version və label saxlanır. Positive label üçün canlı export-da dəqiq process_instance_id tələb olunur; bütöv hostun bütün prosesləri positive işarələnmir. Secret, executable path, argument və connection address datasetə daxil edilmir.

`train.py` bütün hostları final testdən ayırır. CV də bütöv host qrupları ilədir, session-lar qarışmır. Median imputation yalnız training fold-da fit edilir. Random row split, full-data median, hazır scaler, SMOTE-before-CV yoxdur. 200 ağac, depth <=12, minimum leaf=2, fixed random_state istifadə olunur. Precision, recall, F1, confusion matrix və false positive rate çıxarılır.

Model formatı `sentinelzone-forest-json-1`: yalnız bounded numeric ağaclar, train medians və feature list. Pickle/ONNX/native inference runtime yoxdur. Load zamanı SHA256, format, feature version/order/count, finite values, node limits, cycle/unreachable nodes yoxlanır. sklearn float32 conversion eyni tətbiq olunur. Əlavə vector key ignored, required feature missing olduqda availability azalır; coverage <0.5 olduqda prediction verilmir.

Config: `ml_shadow_enabled`, `ml_model_path`, `ml_model_sha256`. Default disabled-dir. Loaded model yalnız `ml={enabled,mode:shadow,model_version,prediction,score,feature_coverage,status}` verir. `assessment_source=heuristic` dəyişmir. Model səhvi agenti dayandırmır və heuristic qərarı dəyişmir. ML score tree leaf fraction-dır, malware probability deyil.

Model manifest SHA256, feature contract, dataset hash/record count, training code commit/hash, creation time və metrics saxlayır. Bu RC üçün real, müxtəlif host/workload üzrə təsdiqlənmiş production model yoxdur. Synthetic pipeline test report yalnız təlim/export mexanizmini yoxlayır; onun yüksək metric-i real detection keyfiyyəti deyil. Model release binaries-ə daxil edilmir, research-only qalır.

Təlim: `python -m pip install -r research/training/requirements.txt`; `python research/training/train.py --dataset DATA.jsonl --features contracts/ml-features.json --output NEW_MODEL_DIR --model-version VERSION --training-code-commit COMMIT`.
