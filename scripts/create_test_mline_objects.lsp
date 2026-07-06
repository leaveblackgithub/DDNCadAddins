;;; ============================================================
;;; create_test_mline_objects.lsp — Create MLINE test objects
;;;   for testing CropMLineService (bounding-box classify)
;;; ============================================================
;;; Usage:
;;;   1. APPLOAD this file
;;;   2. Run CREATETESTMLINE to create all test MLINE objects
;;;   3. Run DELETETESTMLINE to clean up test objects
;;; ============================================================
;;; Note: MLINE uses STANDARD style (2 parallel lines, offset ±0.5).
;;;       Effective width ≈ 1.0 unit.

(vl-load-com)
(setq *test-mline-layer* "AUTOCMDTEST")

;;; ── Ensure test layer exists ──
(defun ensure-mline-test-layer ()
  (setq *acad-doc* (vla-get-activedocument (vlax-get-acad-object)))
  (setq *layers* (vla-get-layers *acad-doc*))
  (setq *layer-obj* nil)
  (vlax-for layer *layers*
    (if (= (vla-get-name layer) *test-mline-layer*)
        (setq *layer-obj* layer)))
  (if (null *layer-obj*)
      (setq *layer-obj* (vla-add *layers* *test-mline-layer*)))
  (princ (strcat "\n  Layer: " *test-mline-layer* " ready")))

;;; ── Create an MLINE via ActiveX ──
;;; vla-addmline requires 3D coordinates: [x1 y1 z1 x2 y2 z2 ...]
(defun create-mline (pts-2d color / *pts-sa* *n* *idx* *mline*)
  ;; pts-2d: list of (x y) pairs, e.g. ((0 0) (100 0))
  (setq *n* (* (length pts-2d) 3))
  (setq *pts-sa* (vlax-make-safearray vlax-vbDouble (cons 0 (1- *n*))))
  (setq *idx* 0)
  (foreach pt pts-2d
    (vlax-safearray-put-element *pts-sa* *idx* (car pt))
    (vlax-safearray-put-element *pts-sa* (1+ *idx*) (cadr pt))
    (vlax-safearray-put-element *pts-sa* (+ *idx* 2) 0.0)
    (setq *idx* (+ *idx* 3)))
  (setq *mline* (vla-addmline *model-space* *pts-sa*))
  (vla-put-layer *mline* *test-mline-layer*)
  (vla-put-color *mline* color)
  (princ (strcat "\n  MLine: " (itoa (length pts-2d)) " vertices, color=" (itoa color))))

;;; ── Main command: create all MLINE test objects ──
(defun C:CREATETESTMLINE (/ *model-space*)
  (princ "\n================================================")
  (princ "\n  Creating MLINE test objects for CropMLineService...")
  (princ "\n  Boundary: 100x100 rect (0,0)-(100,100)")
  (princ "\n  MLINE Style: STANDARD (2 lines, offset ±0.5)")
  (princ "\n================================================")

  (ensure-mline-test-layer)
  (setq *model-space* (vla-get-modelspace *acad-doc*))

  ;; ── Draw boundary rectangle for visual reference ──
  (setq *rect-pts* (vlax-make-safearray vlax-vbDouble (cons 0 7)))
  (vlax-safearray-fill *rect-pts* '(0.0 0.0  100.0 0.0  100.0 100.0  0.0 100.0))
  (setq *rect-pl* (vla-addlightweightpolyline *model-space* *rect-pts*))
  (vla-put-layer *rect-pl* *test-mline-layer*)
  (vla-put-color *rect-pl* 1)
  (vla-put-closed *rect-pl* :vlax-true)
  (princ "\n  Boundary rect: (0,0)-(100,100) color=Red")

  ;; ── 1. MLINE fully INSIDE ──
  ;; Horizontal at y=50, x=20→80. Bounding box approx (20,49.5)-(80,50.5) inside
  (princ "\n  [1] Inside MLine: (20,50) → (80,50) [fully inside]")
  (create-mline (list (list 20 50) (list 80 50)) 2)

  ;; ── 2. MLINE fully OUTSIDE ──
  ;; Horizontal at y=200, x=20→80. Bounding box (20,199.5)-(80,200.5) outside
  (princ "\n  [2] Outside MLine: (20,200) → (80,200) [fully outside]")
  (create-mline (list (list 20 200) (list 80 200)) 3)

  ;; ── 3. MLINE crossing boundary (INTERSECTS) ──
  ;; Vertical at x=50, y=-20→120. Bounding box (49.5,-20)-(50.5,120) crosses boundary
  (princ "\n  [3] Cross MLine: (50,-20) → (50,120) [crosses boundary]")
  (create-mline (list (list 50 -20) (list 50 120)) 4)

  ;; ── 4. MLINE diagonal crossing boundary ──
  ;; Diagonal: (-50,-50) → (150,150). Bounding box crosses both edges
  (princ "\n  [4] Diagonal MLine: (-50,-50) → (150,150) [crosses boundary]")
  (create-mline (list (list -50 -50) (list 150 150)) 5)

  (princ "\n================================================")
  (princ "\n  MLINE test objects created!")
  (princ "\n    #1 (yellow)  Inside:    (20,50)→(80,50)")
  (princ "\n    #2 (green)   Outside:   (20,200)→(80,200)")
  (princ "\n    #3 (cyan)    Cross:     (50,-20)→(50,120)")
  (princ "\n    #4 (blue)    Diagonal:  (-50,-50)→(150,150)")
  (princ "\n  Run DELETETESTMLINE to clean up")
  (princ "\n================================================")
  (princ))

;;; ── Cleanup command: delete all test objects ──
(defun C:DELETETESTMLINE ()
  (princ "\nDeleting all objects on AUTOCMDTEST layer...")
  (setq *ss* (ssget "_X" (list (cons 8 *test-mline-layer*))))
  (if *ss*
      (progn
        (command "_.ERASE" *ss* "")
        (princ (strcat "\nDeleted " (itoa (sslength *ss*)) " objects")))
      (princ "\nNo objects found on AUTOCMDTEST layer"))
  (princ))

(princ "\ncreate_test_mline_objects.lsp loaded")
(princ "\nRun CREATETESTMLINE to create MLINE test objects")
(princ "\nRun DELETETESTMLINE to clean up")
(princ)
