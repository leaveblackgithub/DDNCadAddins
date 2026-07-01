;; ════════════════════════════════════════════════════════════════
;;  test_crop_precision.lsp
;;  验证 SPLINE / ELLIPSE / 3DPOLYLINE 裁剪精度
;;
;;  用法:
;;    1. 新建空白图纸
;;    2. NETLOAD → 选择新编译的 AddinsACAD.dll
;;    3. APPLOAD → 选择本脚本
;;    4. 执行命令: TEST_CROP_PRECISION
;;
;;  脚本自动:
;;    - 创建 SPLINE / ELLIPSE / 3DPOLYLINE 各一条（穿过边界）
;;    - 创建圆形裁剪边界
;;    - 调用 CROPINSIDE 执行裁剪
;;    - 报告裁剪前后实体类型和数量
;; ════════════════════════════════════════════════════════════════

(vl-load-com)

(defun c:TEST_CROP_PRECISION (/ doc ms boundary splineObj ellipseObj poly3dObj ss)
  (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
  (setq ms (vla-get-ModelSpace doc))

  (prompt "\n═══════════════════════════════════════════")
  (prompt "\n   TEST_CROP_PRECISION 开始")
  (prompt "\n═══════════════════════════════════════════")

  ;; ── 1. 创建裁剪边界：圆心 (0,0) 半径 30 的圆 ──
  (setq boundary (vla-AddCircle ms (vlax-3d-point 0 0 0) 30.0))
  (vla-put-Color boundary 1)  ;; 红色
  (prompt "\n[创建] 裁剪边界: 圆 (0,0) R=30")

  ;; ── 2. 创建 SPLINE ──
  (setq splPts (vlax-make-safearray vlax-vbDouble '(0 . 20)))
  (vlax-safearray-fill splPts
    (list -50 -30 -10 10 30 50 60  ;; X
          -20 -10   0 10 20 30 40  ;; Y
           0   0   0  0  0  0  0)) ;; Z
  (setq splineObj (vla-AddSpline ms splPts
                    (vlax-3d-point -1 0 0)  ;; 起点切向
                    (vlax-3d-point  1 0 0))) ;; 终点切向
  (vla-put-Color splineObj 2)  ;; 黄色
  (prompt "\n[创建] SPLINE: 从 (-50,-20) 到 (60,30)")

  ;; ── 3. 创建 ELLIPSE ──
  (setq ellipseObj (vla-AddEllipse ms
                      (vlax-3d-point 0 0 0)
                      (vlax-3d-point 50 0 0)
                      0.5))
  (vla-put-Color ellipseObj 3)  ;; 绿色
  (prompt "\n[创建] ELLIPSE: 中心 (0,0) 长轴 50 短轴 25")

  ;; ── 4. 创建 3DPOLYLINE ──
  (setq p3dPts (vlax-make-safearray vlax-vbDouble '(0 . 20)))
  (vlax-safearray-fill p3dPts
    (list -60 -30 0  30 60 80 100  ;; X
          -30 -15 0  15 30 45  60  ;; Y
           0   0  0   0  0  0   0)) ;; Z
  (setq poly3dObj (vla-Add3DPoly ms p3dPts))
  (vla-put-Color poly3dObj 5)  ;; 蓝色
  (prompt "\n[创建] 3DPOLYLINE: 从 (-60,-30) 到 (100,60)")

  ;; ── 缩放显示 ──
  (vla-ZoomAll (vlax-get-acad-object))
  (prompt "\n\n>>> 已创建测试实体，按 ENTER 执行裁剪...")
  (getstring)

  ;; ── 5. 记录裁剪前的实体信息 ──
  (prompt "\n\n────────── 裁剪前 ──────────")
  (print-obj splineObj  "SPLINE")
  (print-obj ellipseObj "ELLIPSE")
  (print-obj poly3dObj  "3DPOLYLINE")

  ;; ── 6. 执行 CROPINSIDE ──
  (prompt "\n\n>>> 正在执行 CROPINSIDE (保留内部)...")

  ;; 构造选择集：包含 SPLINE + ELLIPSE + 3DPOLYLINE
  (setq ss (ssadd))
  (ssadd (vlax-vla-object->ename splineObj)  ss)
  (ssadd (vlax-vla-object->ename ellipseObj) ss)
  (ssadd (vlax-vla-object->ename poly3dObj)  ss)

  ;; 调用 CROPINSIDE
  (command "_.CROPINSIDE")
  (while (> (getvar "CMDACTIVE") 0)
    (command pause)
  )

  (prompt "\n\nCROPINSIDE 执行完毕。")

  ;; ── 7. 检查裁剪后结果 ──
  (prompt "\n\n────────── 裁剪后 ──────────")
  (check-obj splineObj  "SPLINE")
  (check-obj ellipseObj "ELLIPSE")
  (check-obj poly3dObj  "3DPOLYLINE")

  ;; ── 8. 统计当前图纸中各实体类型数量 ──
  (prompt "\n\n────────── 实体类型统计 ──────────")
  (count-type "AcDbSpline"     "SPLINE")
  (count-type "AcDbEllipse"    "ELLIPSE")
  (count-type "AcDbPolyline3D" "3DPOLYLINE")
  (count-type "AcDbLine"       "LINE")
  (count-type "AcDbPolyline"   "POLYLINE")

  ;; ── 9. 结论 ──
  (prompt "\n\n═══════════════════════════════════════════")
  (prompt "\n   验证结论:")
  (prompt "\n   - 裁剪后 SPLINE/ELLIPSE/3DPOLYLINE 仍保持原类型 = 精确交点 API 生效")
  (prompt "\n   - 裁剪后变成大量 LINE = 旧采样法行为（未更新成功）")
  (prompt "\n═══════════════════════════════════════════")
  (princ)
)

;; ── 辅助函数 ──

(defun print-obj (obj label)
  (if (and obj (vlax-property-available-p obj 'ObjectName))
    (progn
      (prompt (strcat "\n  " label ":"))
      (prompt (strcat "\n    类型 = " (vla-get-ObjectName obj)))
      (prompt (strcat "\n    图层 = " (vla-get-Layer obj)))
    )
    (prompt (strcat "\n  " label ": [已擦除]"))
  )
)

(defun check-obj (obj label)
  (if (and obj (vlax-property-available-p obj 'ObjectName))
    (prompt (strcat "\n  " label ": 仍然存在（未被裁剪）"))
    (prompt (strcat "\n  " label ": [已擦除]（已被裁剪替换）"))
  )
)

(defun count-type (dxfname label / count)
  (setq count 0)
  (vlax-for ent (vla-get-ModelSpace
                 (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName ent) dxfname)
      (setq count (1+ count))
    )
  )
  (prompt (strcat "\n  " label ": " (itoa count) " 个"))
)
