# Mach Diamond 尾焰（URP 体积版）

Shadertoy [Mach Diamonds! (WdGBDc)](https://www.shadertoy.com/view/WdGBDc) 的 Unity / URP 3D 化版本：
沿喷口轴线排布的**激波胞（马赫环）**做成绕轴旋转的**体积**效果，挂在任意 GameObject 上，
跟随其旋转/位移，任意角度（包括从正后方沿轴线看）都有正确的立体视差。

| 文件 | 作用 |
|---|---|
| `MachDiamondPlume.shader` | URP 无光照体积 shader：解析体积求交 + 光线步进 + 逐样本求值原版 SDF 场 |
| `MachDiamondPlumeMesh.cs` | 容器圆柱网格生成器；含 `GameObject > AeroSim > Mach Diamond Plume` 一键搭建菜单 |
| `MachDiamondPlumeController.cs` | 由 `EngineModule` 推力驱动强度/长度，写 MaterialPropertyBlock，按屏幕占比控制步进数 |
| `README.md` | 本文件 |

---

## 1. 一分钟接入

1. 菜单 **GameObject → AeroSim → Mach Diamond Plume**（会以当前选中的对象为父级）。
2. 把生成的 `MachDiamondPlume` 移到**喷口位置**，让它的**本地 +Z 指向尾焰喷出方向**
   （J-10C 尾喷通常就是机体的 `-forward`，所以把它绕 X 轴转 180°）。
3. 在它的 Inspector 上把 **Engine** 拖成飞机的 `EngineModule`（父级链上有 EngineModule 时菜单已自动挂好）。
4. 按需求调 `Nozzle Radius`（默认 0.45 m）与 `Plume Length`（默认 6 m）。

场景里选中它时，Gizmo 画成**管状**（两道端环 + 4 条母线 + 轴线），另外用蓝色小环标出**激波胞位置**，
方便对位和判断胞长是否合适。

> 容器网格与材质会作为资源生成在同目录（`MachDiamondPlumeCylinder.asset` / `MachDiamondPlume.mat`），
> 多架飞机可共用同一个材质（每个实例的差异走 MaterialPropertyBlock）。

**手动 Add Component 也可以**：把 `MachDiamondPlumeController` 加到任意对象上时，编辑器会自动
创建/复用 `MachDiamondPlume.mat` 并赋给它的 MeshRenderer；如果槽里是**运行期临时材质**（旧的
"(runtime)" 材质，不落盘、存场景后会丢），也会被自动换成真正的资源。
组件齿轮菜单 → *Assign Mach Diamond Plume Material* 可手动重跑。两点注意：

* 如果那个对象**本来就有别的材质**（例如你原有的尾焰材质），控制器会把它换掉并打一条警告 ——
  按 **Ctrl+Z** 可以撤销。想两个效果并存，就把尾焰建成**独立子对象**（用菜单），不要挂到同一个 Renderer 上。
* 运行期（打包后）如果 Renderer 上已有别的材质，控制器**不会**覆盖，只会报一次错 —— 材质请在编辑器里挂好。

不要手工改 `MachDiamondPlume` 的 `localScale` —— 控制器把 **transform 缩放当作体积的真实尺寸**在用
（x/y = 半径，z = 长度，单位米）。想整体放大请用 `Size Multiplier`。

---

## 2. 它怎么工作

### 2.1 容器网格 + 解析体积

尾焰本体不是"画在网格表面"的，而是**在一个容器圆柱内部做光线步进**：

* 容器是**封闭**圆柱（半径 1、本地 z ∈ [0,1]、带两端盖），控制器把它缩放成半径 × 长度。
* 着色体积是**解析**的：局部空间中半径 `_ObjScale.x`、长度 `_ObjScale.z` 的圆柱，轴 = 本地 +Z。
  控制器保证网格与解析体积数值一致（同一组数字既设 transform 也设 `_ObjScale`）。
* Shader 用 `Cull Off`（两面都提交），片元里**只保留"解析出射点"那一个**（按"谁的距离更接近 `tFar`"判定），
  于是体积**恰好积分一次**，并且**完全不依赖容器网格的绕序**。
  ⚠️ 早期版本靠 `Cull Front` 隐含依赖绕序：容器**侧壁**三角形的绕序当时是反的，`Cull Front` 于是在侧面渲染的是
  **近侧**壁面，`tFar = min(tFar, fragDist)` 把区间压成 0 → 侧面被 `discard`（"侧面完全不渲染"），
  而端盖绕序是对的，所以正/后方能出画面但只是一张**平亮面**。现在两侧绕序也已修正，且 shader 不再依赖它。
* 片元里射线起点取**该像素的近平面点**（`ComputeWorldSpacePosition(uv, UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP)`），
  方向指向片元。**透视与正交共用同一条路径**（早期版本正交单独推方向时符号写反过，那段逻辑已删除）。

### 2.2 轴对称映射（这是 3D 化的核心）

原版是**侧视 2D**：屏幕 x = 轴向，屏幕 y = 径向，用 vesica（透镜）SDF 链画出激波壳与内部菱形。
移植时把每个采样点由圆柱坐标 `(z, r)` 映射回原版坐标：

```
px = frac((z/unit + _StartPhase) / 0.8) * 0.8 - 0.4      // 轴向，0.8 = 原版胞宽
py = r / unit                                            // 径向
```

关键点：原版 `vesica()` 第一行是 `p = abs(p)`（**x、y 都取绝对值**），所以整个场关于 `y = 0` 对称。
因此**径向距离可以直接当作 py 用**，不需要镜像两份、不需要额外开销，激波壳自然成为**旋转曲面**。

> 这一步是先在 PowerShell 里把原版数学逐行移植、渲染 ASCII 验证过的：
> 修正 `abs(p)` 之后场确实关于 y=0 对称；测得**胞周期 0.8 单位**、**发光半径 ≈0.36 单位**
> （亮体 ≈0.20 单位）。体积半径默认 0.42 单位就是按这个留了余量；控制器用 **0.36** 把"可见半径"
> 映射到喷口半径（用 0.20 会把尾焰做粗近一倍）。

### 2.3 逐样本求值（原版数学 1:1）

每个采样点依次计算：

1. `shell()`：`mvesica` 透镜链的 `smin` 并集 → 尾焰外壳；
2. `diamonds`：小透镜 + 大透镜项 `smin` 后取 `max(...,0)` → 内部菱形；
3. `d = |smin(exhaust, -diamonds, -0.03)|`（负 k 的 smin ≈ 平滑取大 = "挖去"菱形）；
4. `lum = 1 - d * (2*inside + 5*outside/(axial01+0.2))`，再 `pow(lum, 0.9*inside + 1.5*outside)`；
   其中 `inside/outside` 由 `shell()` 的符号决定，`axial01` 替代原版的屏幕 x 比例；
5. **`_FillCut`**：把 `lum` 的实心填充压掉一部分（原版公式在激波壳内部 `d = 0`，是"实心亮体"，
   二维看不出来，沿视线积分就会糊成一根没细节的亮管）；
6. **`_ShockGlow * exp(-d²·_ShockSharp)`**：把**激波面本身**点亮（`d ≈ 0` 处发光）；
7. 轴向包络：`(1 - _AxialDecay·axial01) × tipFade`，尾端平滑收尾，喷口处极短渐入避免硬圆盘；
8. 湍流：原版 4 条 perlin（`t*.5 / t*7 / t*67 / t*101`，幅度 `.03/.01/.01/.005`，并乘 `(0.5+axial01)`）
   与轴向摆动 `0.5*(0.2-|y|)*perlin(t*3)`，作为**坐标扰动**作用在 `(px, py)` 上——和原版扰动 uv 一致；
9. 双色调：`inside` 用 `_TintInside`（默认 1.05/1.00/1.40 蓝紫），`outside` 用 `_TintOutside`（1.40/1.00/1.80），
   再按 `axial01` 减 `_AxialTint`（默认 0/0.2/0.6）→ 下游转暖，还原原版的通道衰减。

### 2.4 积分与混合

按**发射-吸收**模型沿射线累加，并且**在"参考单位"里积分**（`dtU = dt / unit`）——
这样改 `_CellLength` 只改图案尺寸、不会改亮度：

```
acc += T * dens * tint * _Density * dtU
T   *= exp(-dens * _Density * _Absorption * dtU)     // T < 0.004 提前退出
col  = acc * _Intensity                               // 再做 _SoftKnee 软过渡
```

混合是 `Blend One One`（纯加色）、`ZWrite Off`、`ZTest LEqual`、队列 Transparent：
不透明几何会正确遮挡尾焰，输出为 HDR 值 → **直接吃 URP 的 Bloom**。步进起点带抖动（interleaved gradient noise），消除分层条纹。

---

## 3. 参数速查

**几何**

| 参数 | 默认 | 说明 |
|---|---|---|
| `sizeFromNozzleRadius` | on | 可见尾焰半径 ≈ 喷口半径。用的是**实测可见半径 0.36 参考单位**（不是亮体 0.20，那样会粗一倍），胞长随之推导：0.45 m 喷口 → 胞长 ≈ 1.0 m |
| `nozzleRadius` | 0.45 m | J-10C 量级；最直观的"大小"旋钮 |
| `cellLength` | 1.0 m | 关掉上一项时直接指定激波胞长度（= 原版 0.8 单位） |
| `plumeLength` | 6 m | 全加力时的长度；干推时按 `dryLengthFactor` 缩短 |
| `volumeRadiusUnits` | 0.42 | 体积可见半径（参考单位）。原版发光到 ~0.36，留余量 |
| `sizeMultiplier` | 1 | 整体缩放（不要动 transform.localScale） |

**强度**

| 参数 | 默认 | 说明 |
|---|---|---|
| `dryIntensity` | 0.18 | 干推（军用推力）时的亮度 |
| `afterburnerIntensity` | 1.4 | 全加力亮度（HDR） |
| `smoothing` | 7 | 响应速度 1/s，0 = 瞬时 |
| `_Density` / `_Absorption` | 1.0 / 0.9 | 介质浓度与自吸收。吸收越大，越"看进去才亮"、核心越不容易糊成白团 |
| `_ShockGlow` | 1.0 | 激波面自发光强度，想要更硬的马赫环就调大 |
| `_SoftKnee` | 1.2 | 高光软过渡（HDR 单位）：核心超过该值后按 `knee·(1-e^{-x/knee})` 收敛，避免整块核心被削平成纯白；0 = 关闭 |
| `_MaxEmission` | 8 | HDR 上限保护 |

**结构 / 颜色**：`_FillCut`（实心填充削减）、`_ShockSharp`（激波面锐度）、`_DiamondCarve`（菱形挖暗强度）、
`_TintInside/_TintOutside/_AxialTint`（双色调与下游偏色）、`_AxialDecay/_TipStart/_TipPower/_ExitFade`（轴向包络）、
`_Turbulence/_FlowSpeed/_NoiseSpan/_Wobble/_Seed`（湍流）。

**性能**：`_Steps`（步进数，控制器自动按屏幕占比在 `minSteps..maxSteps` 之间调）、
`_SOFT_PARTICLES` + `_SoftFade`（软粒子，见第 7 节）。

---

## 4. 看起来不对时，按这个顺序调

下面两条结论来自我在没开 Unity 的情况下用 CPU 复算这套数学（同密度场 + 同样的逐步积分），默认值就是据此定的：

1. **核心过曝成一坨白**：密度场峰值 **1.52**，沿轴最长弦（~6 m）在 `_Absorption = 0.65` 下能积到 **~3 HDR**，
   再乘 `_Intensity = 1.9` 就整块压到白色（旧默认值是 0.65 / 1.9 / clamp 12）。
   → 现在 `_Absorption 0.9` + `_SoftKnee 1.2` + `afterburnerIntensity 1.4`。
   **还嫌白**：先降 `Afterburner Intensity`，再降 `_Density`；**嫌暗**：加 `_Intensity`（有软过渡不会突然爆）。
2. **像一根没有细节的亮管**：原版在激波壳内部 `d = 0`，是"实心填充"，一积分就把激波结构糊平。
   → 现在 `_FillCut 0.55` + `_ShockGlow 1.0`。
   **想要更硬更清楚的马赫环**：`_ShockGlow` 1.5~2.5、`_FillCut` 0.7~0.85；
   **想更接近原版那种"实心亮体"**：`_FillCut → 0`、`_ShockGlow → 0.3`。
3. **环太少 / 太胖**：默认 0.45 m 喷口 → 胞长 1.0 m、可见半径 0.45 m，6 m 上约 6 个环。
   想更密：关掉 `Size From Nozzle Radius` 直接填 `Cell Length`（例如 0.7 m）；尾焰会同时变细
   （两者的比例被原版图案的 0.36:0.8 固定住，不能独立调）。
4. **位置/朝向不对**：移到喷口、本地 **+Z 对准喷流方向**，看 Gizmo 的蓝色胞标记是否沿轴向排布。

---

## 5. 性能

* 每条尾焰 **1 个 draw call**，加色、不写深度、不投影、不参与反射探针与运动矢量（控制器已设好）。
* 成本 ≈ `步进数 × (1 次图案求值 + 4 次 perlin)`。默认 36 步、屏幕占比小时自动降到 12 步；
  远处的飞机几乎不花钱。`_Steps` 变化时才重写 MPB。
* 加力关闭时控制器直接 `renderer.enabled = false`，**完全零成本**。
* 建议：多架带尾焰的飞机保持 `maxSteps ≤ 36`；只在主角机上开到 48+。
* 想再省：`_Turbulence` 设 0（perlin 分支被跳过）、`_Absorption` 调大让提前退出更早生效、
  或把 `fullDetailFraction` 调大（更早降到低步数）。

---

## 6. 与原版 2D 的差异（3 处，都是有意的）

1. **`_FillCut` + `_ShockGlow`**：原版是"亮体内挖暗菱形"，体积积分会把结构填平（CPU 复算确认会退化成亮管），
   所以削减实心填充并把激波面点亮。`_FillCut = 0` 且 `_ShockGlow = 0.3` 基本等价于纯原版公式。
2. **无时间门控**：原版用 `smoothstep(start/end, t)` 跟音乐同步（3.1s→23.6s 出现/消失），
   这里换成由推力驱动的 `_Intensity`。
3. **丢弃 `streams` 死代码**：原版那个 6 次循环算出的 `streams` 最终乘的是 `1.`，对画面无影响。

另外，原版 `col -= diamonds` 是从**颜色**里减，这里改为从**密度**里减（体积里更自洽，视觉一致）。

---

## 7. 已知限制

* **软粒子需要深度纹理**：项目当前三套 URP 资产（Performant / Balanced / High Fidelity）的
  `Depth Texture` 都是关闭的。要获得"被机身/地面柔和切开"的效果，
  请在 **Project Settings → Graphics/Quality 对应 URP Asset → 勾选 Depth Texture**，
  再在材质上勾选 **Soft Particles**（或 `material.EnableKeyword("_SOFT_PARTICLES")`）。
  不勾也能用：靠 `ZTest LEqual` 保证不透明几何仍然遮挡，只是交界处硬边。
* **相机在尾焰内部**：出射判定改成"解析距离"后，相机钻进尾焰也只会有一个片元被着色
  （`tNear` 被夹到 0，从相机处开始积分），不再有多面叠加偏亮的问题。
* **沿轴看就是一张圆盘**：这是轴对称尾焰的固有结果（所有视线几乎平行于轴、穿过同样的柱体），
  真实加力尾焰正对着看也是亮盘。稍微偏一点角度就能看到一圈圈激波胞。
* **MFD 等离屏正交相机会渲染它**：若不想在 MFD 里出现，给尾焰单独设 Layer，并在 MFD 相机的
  `cullingMask` 里排除。
* 尾焰为**加色**、不写深度：多个尾焰互相叠加、与其它透明特效的前后关系只由队列决定（加色下通常无感）。

---

## 8. 和现有尾焰的关系

`EngineModule.flameMainEffect` / `flame.shadergraph` 的旧尾焰可以并存：
把 `MachDiamondPlume` 作为另一个子对象挂在喷口即可，用 `dryIntensity/afterburnerIntensity`
把两者亮度曲线对齐，避免加力瞬间"两套尾焰各亮各的"。
若想彻底替换，把 `EngineModule.flameMainEffect` 指向尾焰对象（控制器不依赖它，纯可选）。
