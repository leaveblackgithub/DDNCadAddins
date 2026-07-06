;;; ============================================================
;;; create_test_block_objects.lsp — Create BLOCK test objects
;;;   for testing CropBlockService (bounding-box classify)
;;; ============================================================
;;; Usage:
;;;   1. APPLOAD this file
;;;   2. Run CREATETESTBLOCKS to create all test block objects
;;;   3. Run DELETETESTBLOCKS to clean up test block objects
;;; ============================================================

(vl-load-com)
(setq *test-block-layer* "AUTOCMDTEST")

;;; ── Ensure test layer exists ──
(defun ensure-block-test-layer ()
  (setq *acad-doc* (vla-get-activedocument (vlax-get-acad-object)))
  (setq *layers* (vla-get-layers *acad-doc*))
  (setq *layer-obj* nil)
  (vlax-for layer *layers*
    (if (= (vla-get-name layer) *test-block-layer*)
        (setq *layer-obj* layer)))
  (if (null *layer-obj*)
      (setq *layer-obj* (vla-add *layers* *test-block-layer*)))
  (princ (strcat "\n  Layer: " *test-block-layer* " ready")))

;;; ── Create a simple block definition: 30x30 rectangle (centered at 0,0) ──
;;; Block extent: (-15,-15) to (15,15), so bounding box is 30x30
(defun create-block-def (block-name / *blk-def* *entities* *line* *block-obj*)
  ;; Create block definition in BlockTable
  (setq *blk-def* (vla-add (vla-get-blocks *acad-doc*)
    (vlax-3d-point 0 0 0) block-name))
  (princ (strcat "\n  Creating block definition: " block-name))

  ;; Draw 4 lines forming a 30x30 rectangle centered at origin
  ;; Bottom: (-15,-15) → (15,-15)
  (setq *line* (vla-addline *blk-def*
    (vlax-3d-point -15 -15 0) (vlax-3d-point 15 -15 0)))
  (vla-put-layer *line* "0")
  (vla-put-color *line* acByLayer)

  ;; Right: (15,-15) → (15,15)
  (setq *line* (vla-addline *blk-def*
    (vlax-3d-point 15 -15 0) (vlax-3d-point 15 15 0)))
  (vla-put-layer *line* "0")
  (vla-put-color *line* acByLayer)

  ;; Top: (15,15) → (-15,15)
  (setq *line* (vla-addline *blk-def*
    (vlax-3d-point 15 15 0) (vlax-3d-point -15 15 0)))
  (vla-put-layer *line* "0")
  (vla-put-color *line* acByLayer)

  ;; Left: (-15,15) → (-15,-15)
  (setq *line* (vla-addline *blk-def*
    (vlax-3d-point -15 15 0) (vlax-3d-point -15 -15 0)))
  (vla-put-layer *line* "0")
  (vla-put-color *line* acByLayer)

  (princ (strcat "\n  Block " block-name " defined: 30x30 rectangle")))

;;; ── Insert block reference at given point ──
(defun insert-block-ref (block-name ins-x ins-y / *blk-ref*)
  (setq *blk-ref* (vla-insertblock *model-space*
    (vlax-3d-point ins-x ins-y 0) block-name 1.0 1.0 1.0 0.0))
  (vla-put-layer *blk-ref* *test-block-layer*)
  (vla-put-color *blk-ref* acByLayer)
  (princ (strcat "\n  BlockRef " block-name " at (" (rtos ins-x 2 0) "," (rtos ins-y 2 0) ")")))

;;; ── Main command: create all test block objects ──
(defun C:CREATETESTBLOCKS (/ *model-space*)
  (princ "\n================================================")
  (princ "\n  Creating BLOCK test objects for CropBlockService...")
  (princ "\n  Boundary: 100x100 rect (0,0)-(100,100)")
  (princ "\n================================================")

  (ensure-block-test-layer)
  (setq *model-space* (vla-get-modelspace *acad-doc*))

  ;; ── Draw boundary rectangle for visual reference ──
  (setq *rect-pts* (vlax-make-safearray vlax-vbDouble (cons 0 7)))
  (vlax-safearray-fill *rect-pts* '(0.0 0.0  100.0 0.0  100.0 100.0  0.0 100.0))
  (setq *rect-pl* (vla-addlightweightpolyline *model-space* *rect-pts*))
  (vla-put-layer *rect-pl* *test-block-layer*)
  (vla-put-color *rect-pl* 1)
  (vla-put-closed *rect-pl* :vlax-true)
  (princ "\n  Boundary rect: (0,0)-(100,100) color=Red")

  ;; ── Create block definition ──
  (create-block-def "TEST_BLOCK_CROP")

  ;; ── Insert at (50,50) → bounding box (35,35)-(65,65) FULLY INSIDE ──
  (insert-block-ref "TEST_BLOCK_CROP" 50 50)

  ;; ── Insert at (200,200) → bounding box (185,185)-(215,215) FULLY OUTSIDE ──
  (insert-block-ref "TEST_BLOCK_CROP" 200 200)

  ;; ── Insert at (90,90) → bounding box (75,75)-(105,105) INTERSECTS boundary ──
  (insert-block-ref "TEST_BLOCK_CROP" 90 90)

  (princ "\n================================================")
  (princ "\n  BLOCK test objects created!")
  (princ "\n    TEST_BLOCK_CROP at (50,50)   ← Inside boundary")
  (princ "\n    TEST_BLOCK_CROP at (200,200) ← Outside boundary")
  (princ "\n    TEST_BLOCK_CROP at (90,90)   ← Intersects boundary")
  (princ "\n  Run DELETETESTBLOCKS to clean up")
  (princ "\n================================================")
  (princ))

;;; ── Cleanup command: delete all test block objects ──
(defun C:DELETETESTBLOCKS ()
  (princ "\nDeleting all objects on AUTOCMDTEST layer...")
  (setq *ss* (ssget "_X" (list (cons 8 *test-block-layer*))))
  (if *ss*
      (progn
        (command "_.ERASE" *ss* "")
        (princ (strcat "\nDeleted " (itoa (sslength *ss*)) " objects")))
      (princ "\nNo objects found on AUTOCMDTEST layer"))
  (princ))
;;; ── Also purge the block definition ──
;;; Note: AutoCAD will not purge block def if refs exist; DELETETESTBLOCKS erases refs first,
;;; then running PURGE manually will remove the def.

(princ "\ncreate_test_block_objects.lsp loaded")
(princ "\nRun CREATETESTBLOCKS to create block test objects")
(princ "\nRun DELETETESTBLOCKS to clean up")
(princ)
