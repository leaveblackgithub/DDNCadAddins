;;; ============================================================
;;; create_test_objects.lsp — Create AUTOCMDTEST test objects
;;; ============================================================
;;; Usage:
;;;   1. APPLOAD this file
;;;   2. Run CREATETESTOBJECTS to create all test objects
;;;   3. Run DELETETESTOBJECTS to clean up test objects
;;; ============================================================

(vl-load-com)
(setq *test-layer* "AUTOCMDTEST")

;;; ── Ensure test layer exists ──
(defun ensure-test-layer ()
  (setq *acad-doc* (vla-get-activedocument (vlax-get-acad-object)))
  (setq *layers* (vla-get-layers *acad-doc*))
  (setq *layer-obj* nil)
  (vlax-for layer *layers*
    (if (= (vla-get-name layer) *test-layer*)
        (setq *layer-obj* layer)))
  (if (null *layer-obj*)
      (setq *layer-obj* (vla-add *layers* *test-layer*)))
  (princ (strcat "\n  Layer: " *test-layer* " ready")))

;;; ── Create line ──
(defun create-line (x1 y1 x2 y2 color)
  (setq *line* (vla-addline *model-space*
    (vlax-3d-point x1 y1 0) (vlax-3d-point x2 y2 0)))
  (vla-put-layer *line* *test-layer*)
  (vla-put-color *line* color)
  (princ (strcat "\n  Line: (" (rtos x1 2 0) "," (rtos y1 2 0) ")-("
                 (rtos x2 2 0) "," (rtos y2 2 0) ")")))

;;; ── Create arc ──
(defun create-arc (cx cy radius start-angle end-angle color)
  (setq *arc* (vla-addarc *model-space*
    (vlax-3d-point cx cy 0) radius start-angle end-angle))
  (vla-put-layer *arc* *test-layer*)
  (vla-put-color *arc* color)
  (princ (strcat "\n  Arc: center(" (rtos cx 2 0) "," (rtos cy 2 0)
                 ") radius" (rtos radius 2 0))))

;;; ── Create circle ──
(defun create-circle (cx cy radius color)
  (setq *circle* (vla-addcircle *model-space*
    (vlax-3d-point cx cy 0) radius))
  (vla-put-layer *circle* *test-layer*)
  (vla-put-color *circle* color)
  (princ (strcat "\n  Circle: center(" (rtos cx 2 0) "," (rtos cy 2 0)
                 ") radius" (rtos radius 2 0))))

;;; ── Create polyline ──
(defun create-polyline (points color)
  (setq *pts* (vlax-make-safearray vlax-vbDouble (cons 0 (1- (* (length points) 2)))))
  (setq *idx* 0)
  (foreach pt points
    (vlax-safearray-put-element *pts* *idx* (car pt))
    (vlax-safearray-put-element *pts* (1+ *idx*) (cadr pt))
    (setq *idx* (+ *idx* 2)))
  (setq *pl* (vla-addlightweightpolyline *model-space* *pts*))
  (vla-put-layer *pl* *test-layer*)
  (vla-put-color *pl* color)
  (princ "\n  Polyline: created"))

;;; ── Create spline ──
(defun create-spline (points color)
  (setq *n* (length points))
  (setq *fit-pts* (vlax-make-safearray vlax-vbDouble (cons 0 (1- (* *n* 3)))))
  (setq *idx* 0)
  (foreach pt points
    (vlax-safearray-put-element *fit-pts* *idx* (car pt))
    (vlax-safearray-put-element *fit-pts* (1+ *idx*) (cadr pt))
    (vlax-safearray-put-element *fit-pts* (+ *idx* 2) 0.0)
    (setq *idx* (+ *idx* 3)))
  (setq *spline* (vla-addspline *model-space* *fit-pts*
    (vlax-3d-point 0 0 0) (vlax-3d-point 0 0 0)))
  (vla-put-layer *spline* *test-layer*)
  (vla-put-color *spline* color)
  (princ "\n  Spline: created"))

;;; ── Create ellipse ──
(defun create-ellipse (cx cy maj-x maj-y ratio color)
  (setq *ellipse* (vla-addellipse *model-space*
    (vlax-3d-point cx cy 0)
    (vlax-3d-point maj-x maj-y 0) ratio))
  (vla-put-layer *ellipse* *test-layer*)
  (vla-put-color *ellipse* color)
  (princ (strcat "\n  Ellipse: center(" (rtos cx 2 0) "," (rtos cy 2 0) ")")))

;;; ── Create text ──
(defun create-text (x y str height color)
  (setq *text* (vla-addtext *model-space* str
    (vlax-3d-point x y 0) height))
  (vla-put-layer *text* *test-layer*)
  (vla-put-color *text* color)
  (princ (strcat "\n  Text: \"" str "\" at (" (rtos x 2 0) "," (rtos y 2 0) ")")))

;;; ── Create mtext ──
(defun create-mtext (x y str height width color)
  (setq *mtext* (vla-addmtext *model-space*
    (vlax-3d-point x y 0) width str))
  (vla-put-layer *mtext* *test-layer*)
  (vla-put-color *mtext* color)
  (vla-put-height *mtext* height)
  (princ (strcat "\n  MText: \"" str "\" at (" (rtos x 2 0) "," (rtos y 2 0) ")")))

;;; ── Create point ──
(defun create-point (x y color)
  (setq *pt* (vla-addpoint *model-space* (vlax-3d-point x y 0)))
  (vla-put-layer *pt* *test-layer*)
  (vla-put-color *pt* color)
  (princ (strcat "\n  Point: (" (rtos x 2 0) "," (rtos y 2 0) ")")))

;;; ── Create hatch using command function (most reliable in LSP) ──
(defun create-hatch (cx cy radius)
  ;; Draw circle first as boundary
  (command "_.CIRCLE" (list cx cy) radius)
  (setq *circ-ent* (entlast))
  (vla-put-layer (vlax-ename->vla-object *circ-ent*) *test-layer*)
  (vla-put-color (vlax-ename->vla-object *circ-ent*) 3)
  ;; Create hatch with the circle as boundary
  (command "_.-HATCH" "_P" "ANGLE" "" "" "_S" *circ-ent* "" "")
  (setq *hatch-ent* (entlast))
  (if *hatch-ent*
      (progn
        (vla-put-layer (vlax-ename->vla-object *hatch-ent*) *test-layer*)
        (vla-put-color (vlax-ename->vla-object *hatch-ent*) 2)
        (princ (strcat "\n  Hatch: center(" (rtos cx 2 0) "," (rtos cy 2 0)
                       ") radius" (rtos radius 2 0) " (ANGLE)")))
      (princ "\n  Hatch: creation failed")))

;;; ── Main command: create all test objects ──
(defun C:CREATETESTOBJECTS (/ *model-space*)
  (princ "\n================================================")
  (princ "\n  Creating AUTOCMDTEST test objects...")
  (princ "\n================================================")

  (ensure-test-layer)
  (setq *model-space* (vla-get-modelspace *acad-doc*))

  ;; Crop boundary: 100x100 rectangle
  (setq *rect-pts* (list (list 0 0) (list 100 0) (list 100 100) (list 0 100)))
  (setq *rect* (create-polyline *rect-pts* 1))
  (setq *last-obj* (entlast))
  (command "_.PEDIT" *last-obj* "_C" "")
  (princ "\n  Boundary: 100x100 rect (0,0)-(100,100)")

  ;; Test entities
  (create-line 50 50 80 80 2)        ;; fully inside
  (create-line 50 -20 50 120 3)      ;; crossing boundary
  (create-arc 30 30 20 0 pi 4)       ;; arc
  (create-circle 70 30 15 5)         ;; circle
  (create-polyline (list (list 20 50) (list 50 80) (list 80 50) (list 110 80)) 6)  ;; polyline
  (create-spline (list (list -20 20) (list 30 10) (list 70 40) (list 120 20)) 7)  ;; spline
  (create-ellipse 30 70 20 0 0.5 8)  ;; ellipse
  (create-text 10 10 "TEST" 5 9)     ;; text
  (create-mtext 60 10 "MText Test" 5 40 10)  ;; mtext
  (create-point 50 50 11)            ;; point

  ;; Hatch
  (create-hatch 150 50 30)

  (princ "\n================================================")
  (princ "\n  All test objects created! Layer: AUTOCMDTEST")
  (princ "\n  Use LAYOFF to hide AUTOCMDTEST layer")
  (princ "\n================================================")
  (princ))

;;; ── Cleanup command: delete all test objects ──
(defun C:DELETETESTOBJECTS ()
  (princ "\nDeleting all objects on AUTOCMDTEST layer...")
  (setq *ss* (ssget "_X" (list (cons 8 *test-layer*))))
  (if *ss*
      (progn
        (command "_.ERASE" *ss* "")
        (princ (strcat "\nDeleted " (itoa (sslength *ss*)) " objects")))
      (princ "\nNo objects found on AUTOCMDTEST layer"))
  (princ))

(princ "\ncreate_test_objects.lsp loaded")
(princ "\nRun CREATETESTOBJECTS to create test objects")
(princ "\nRun DELETETESTOBJECTS to clean up test objects")
(princ)
