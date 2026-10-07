# ML shadow qiymətləndirməsi

15 runtime ML testi hər iki OS-də keçdi. Hash mismatch, malformed JSON, null model arrays, feature contract/version/order, cycle, missing/extra feature, coverage abstention və heuristic qərarına təsir etməməsi yoxlanıldı. Python metodologiya suite-i 6/6 keçdi: host/session ayrılığı, train-only imputation, confusion/FPR hesablaması və numeric export-un sklearn nəticəsinə uyğunluğu.

Öz training pipeline-ımız 640 **açıq işarələnmiş sintetik fixture** ilə işlədildi: 8 synthetic host, 32 session, 480 train / 160 test record. Final testdə precision=1, recall=1, F1=1 və FPR=0 alındı; bu rəqəmlər yalnız sadə pipeline fixture-ni təsdiqləyir, real workload keyfiyyəti deyil. Host-group CV və final host holdout arasında leakage yoxdur.

Model `0.22.2-rc.1-pipeline-fixture-v2`, yalnız `validation/research-only/` altında research artifact-dır. Agent paketində avtomatik yüklənən model yoxdur; default ML disabled, enabled olduqda shadow-dur. Native Windows və Linux həmin forest ilə eyni input üçün eyni prediction/score/coverage verdi. `assessment_source=heuristic` qalır.

Model hash-i, feature version, dataset hash/record sayı, training source SHA256, creation time və metrics manifestdədir. Workspace git commit olmadığı üçün training commit `WORKTREE-UNCOMMITTED` kimi açıq qeyd edilir. Real host/workload dataset, model promotion və production ML acceptance **UNVERIFIED** qalır.
