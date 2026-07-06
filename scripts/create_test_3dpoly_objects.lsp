;;; ============================================================
;;; create_test_3dpoly_objects.lsp — Create 3DPOLYLINE test objects
;;;   for testing Crop3DPolylineService (param-search split)
;;; ============================================================
;;; Usage:
;;;   1. APPLOAD this file
;;;   2. Run CREATETEST3DPOLY to create all test 3D Polyline objects
;;;   3. Run DELETETEST3DPOLY to clean up test objects
;;; ============================================================

(vl-load-com)
(setq *test-3dpoly-layer* "AUTOCMDTEST")

;;; ── Ensure test layer exists ──
(defun ensure-3dpoly-test-layer ()
  (setq *acad-doc* (vla-get-activedocument (vlax-get-acad-object)))
  (setq *layers* (vla-get-layers *acad-doc*))
  (setq *layer-obj* nil)
  (vlax-for layer *layers*
    (if (= (vla-get-name layer) *test-3dpoly-layer*)
        (setq *layer-obj* layer)))
  (if (null *layer-obj*)
      (setq *layer-obj* (vla-add *layers* *test-3dpoly-layer*)))
  (princ (strcat "\n  Layer: " *test-3dpoly-layer* " ready")))

;;; ── Create a 3D Polyline from list of (x y z) points ──
(defun create-3dpoly (pts-list color / *pts-sa* *idx* *3dpoly*)
  ;; pts-list: list of (x y z) lists, e.g. ((0 0 0) (10 10 5) (20 0 0))
  (setq *npts* (length pts-list))
  (setq *pts-sa* (vlax-make-safearray vlax-vbDouble (cons 0 (1- (* *npts* 3)))))
  (setq *idx* 0)
  (foreach pt pts-list
    (vlax-safearray-put-element *pts-sa* *idx* (car pt))
    (vlax-safearray-put-element *pts-sa* (1+ *idx*) (cadr pt))
    (vlax-safearray-put-element *pts-sa* (+ *idx* 2) (caddr pt))
    (setq *idx* (+ *idx* 3)))
  (setq *3dpoly* (vla-add3dpoly *model-space* *pts-sa*))
  (vla-put-layer *3dpoly* *test-3dpoly-layer*)
  (vla-put-color *3dpoly* color)
  (if (vlax-property-available-p *3dpoly* 'Closed)
      (vla-put-closed *3dpoly* :vlax-false))
  (princ (strcat "\n  3DPoly: " (itoa *npts*) " vertices, color=" (itoa color))))

;;; ── Main command: create all 3D Polyline test objects ──
(defun C:CREATETEST3DPOLY (/ *model-space*)
  (princ "\n================================================")
  (princ "\n  Creating 3DPOLYLINE test objects for Crop3DPolylineService...")
  (princ "\n  Boundary: 100x100 rect (0,0)-(100,100)")
  (princ "\n================================================")

  (ensure-3dpoly-test-layer)
  (setq *model-space* (vla-get-modelspace *acad-doc*))

  ;; ── Draw boundary rectangle for visual reference ──
  (setq *rect-pts* (vlax-make-safearray vlax-vbDouble (cons 0 7)))
  (vlax-safearray-fill *rect-pts* '(0.0 0.0  100.0 0.0  100.0 100.0  0.0 100.0))
  (setq *rect-pl* (vla-addlightweightpolyline *model-space* *rect-pts*))
  (vla-put-layer *rect-pl* *test-3dpoly-layer*)
  (vla-put-color *rect-pl* 1)
  (vla-put-closed *rect-pl* :vlax-true)
  (princ "\n  Boundary rect: (0,0)-(100,100) color=Red")

  ;; ── 1. 3D Polyline fully INSIDE ──
  ;; Points: (20,20,0) → (50,30,5) → (80,60,10) → (60,80,5) → (30,70,0)
  ;; Bounding box approx (20,20)-(80,80), well within (0,0)-(100,100)
  (princ "\n  [1] Inside 3DPoly: (20,20) → (80,60) → (60,80) → (30,70)")
  (create-3dpoly (list (list 20 20 0) (list 50 30 5) (list 80 60 10)
                       (list 60 80 5) (list 30 70 0)) 2)

  ;; ── 2. 3D Polyline fully OUTSIDE ──
  ;; Points: (200,200,0) → (250,220,5) → (280,250,10) → (220,300,5)
  ;; Bounding box (200,200)-(280,300), fully outside
  (princ "\n  [2] Outside 3DPoly: (200,200) → (280,250) → (220,300)")
  (create-3dpoly (list (list 200 200 0) (list 250 220 5)
                       (list 280 250 10) (list 220 300 5)) 3)

  ;; ── 3. 3D Polyline crossing boundary (INTERSECTS) ──
  ;; Points: (-50,50,0) → (30,50,5) → (80,50,10) → (150,50,5)
  ;; Horizontal line crossing the 100x100 boundary at x=0 and x=100
  (princ "\n  [3] Cross 3DPoly: (-50,50) → (30,50) → (80,50) → (150,50)")
  (create-3dpoly (list (list -50 50 0) (list 30 50 5)
                       (list 80 50 10) (list 150 50 5)) 4)

  ;; ── 4. 3D Polyline crossing boundary diagonally ──
  ;; Points: (-50,-50,0) → (40,40,5) → (120,120,10)
  ;; Diagonal crossing the boundary at (0,0) and (100,100)
  (princ "\n  [4] Diagonal 3DPoly: (-50,-50) → (40,40) → (120,120)")
  (create-3dpoly (list (list -50 -50 0) (list 40 40 5) (list 120 120 10)) 5)

  ;; ── 5. 3D Polyline with endpoint ON boundary ──
  ;; Points: (0,30,0) → (30,30,5) → (60,30,10) → (100,30,5)
  ;; Starts at x=0 (left boundary edge), ends at x=100 (right boundary edge)
  (princ "\n  [5] Edge 3DPoly: (0,30) → (60,30) → (100,30) [on boundaries]")
  (create-3dpoly (list (list 0 30 0) (list 30 30 5)
                       (list 60 30 10) (list 100 30 5)) 6)

  (princ "\n================================================")
  (princ "\n  3DPOLYLINE test objects created!")
  (princ "\n    #1 (yellow)  Inside:     (20,20)→(80,60)→(60,80)→(30,70)")
  (princ "\n    #2 (green)   Outside:    (200,200)→(280,250)→(220,300)")
  (princ "\n    #3 (cyan)    Cross horz: (-50,50)→(150,50)")
  (princ "\n    #4 (blue)    Cross diag: (-50,-50)→(120,120)")
  (princ "\n    #5 (magenta) Edge:       (0,30)→(100,30)")
  (princ "\n  Run DELETETEST3DPOLY to clean up")
  (princ "\n================================================")
  (princ))

;;; ── Cleanup command: delete all test objects ──
(defun C:DELETETEST3DPOLY ()
  (princ "\nDeleting all objects on AUTOCMDTEST layer...")
  (setq *ss* (ssget "_X" (list (cons 8 *test-3dpoly-layer*))))
  (if *ss*
      (progn
        (command "_.ERASE" *ss* "")
        (princ (strcat "\nDeleted " (itoa (sslength *ss*)) " objects")))
      (princ "\nNo objects found on AUTOCMDTEST layer"))
  (princ))

(princ "\ncreate_test_3dpoly_objects.lsp loaded")
(princ "\nRun CREATETEST3DPOLY to create 3D Polyline test objects")
(princ "\nRun DELETETEST3DPOLY to clean up")
(princ)
