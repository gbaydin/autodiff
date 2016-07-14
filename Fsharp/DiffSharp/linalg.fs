﻿[<ReflectedDefinition>]
module linalg

open utils
open types

let inline mult_by_scalar (x: Vector) (y: Number): Vector =
    arrayMap (fun a -> a*y) x

let inline cross (a: Vector) (b: Vector) =
    [| a.[1]*b.[2] - a.[2]*b.[1]; a.[2]*b.[0] - a.[0]*b.[2]; a.[0]*b.[1] - a.[1]*b.[0]; |]

let inline add_vec (x: Vector) (y: Vector) =
    arrayMap2 (+) x y

let inline add_vec3 (x: Vector) (y: Vector) (z: Vector) =
    add_vec (add_vec x y) z

let inline sub_vec (x: Vector) (y: Vector) =
    arrayMap2 (-) x y

[<DontInline>]
let inline sqnorm (x: Vector) =
    arraySum (arrayMap (fun x1 -> x1*x1) x)

let inline dot_prod (x: Vector) (y: Vector) =
    arraySum (arrayMap2 (*) x y)

let radial_distort (rad_params: Vector) (proj: Vector) =
    let rsq = sqnorm proj
    let L = 1. + rad_params.[0] * rsq + rad_params.[1] * rsq * rsq
    mult_by_scalar proj L

let rodrigues_rotate_point (rot: Vector) (x: Vector) =
    let sqtheta = sqnorm rot
    if sqtheta <> 0. then
      let theta = sqrt sqtheta
      let costheta = cos theta
      let sintheta = sin theta
      let theta_inv = 1. / theta
      let w = mult_by_scalar rot theta_inv
      let w_cross_X = cross w x    
      let tmp = (dot_prod w x) * (1. - costheta)
      let v1 = mult_by_scalar x costheta
      let v2 = mult_by_scalar w_cross_X sintheta 
      add_vec (add_vec v1 v2) (mult_by_scalar w tmp)
    else 
      add_vec x (cross rot x)

let project (cam: Vector) (x: Vector) =
    (* Should be changed to a global constant variable *)
    let N_CAM_PARAMS = 11
    let ROT_IDX = 0
    let CENTER_IDX = 3
    let FOCAL_IDX = 6
    let X0_IDX = 7
    let RAD_IDX = 9
    let Xcam = rodrigues_rotate_point 
                  cam.[ROT_IDX..(ROT_IDX+2)]  
                  (sub_vec x cam.[CENTER_IDX..(CENTER_IDX+2)])
    let distorted = radial_distort 
                      cam.[RAD_IDX..(RAD_IDX+1)] 
                      (mult_by_scalar Xcam.[0..1] (1./Xcam.[2]))
    add_vec 
        cam.[X0_IDX..(X0_IDX+1)] 
        (mult_by_scalar distorted cam.[FOCAL_IDX])

let compute_reproj_err (cam: Vector) (x: Vector) (w: Number) (feat: Vector) =
    mult_by_scalar (sub_vec (project cam x) feat) w

let compute_zach_weight_error w =
    1. - w*w

let w_err (w:Vector) = 
    arrayMap compute_zach_weight_error w 

let reproj_err (cams:Matrix) (x:Matrix) (w:Vector) (obs:Matrix) (feat:Matrix): Matrix =
    let n = cams.Length
    let p = w.Length
    let range = arrayRange 0 (p - 1)
    arrayMapToMatrix (fun i -> compute_reproj_err cams.[int obs.[int i].[0]] x.[int obs.[int i].[1]] w.[int i] feat.[int i]) range

let vectorRead (fn: string) (startLine: Index): Vector = 
    let matrix = matrixRead fn startLine 1
    matrix.[0]

let numberRead (fn: string) (startLine: Index): Number = 
    let vector = vectorRead fn startLine
    vector.[0]

let run_ba_from_file (fn: string) = 
    let nmp = vectorRead fn 0
    let n = int nmp.[0]
    let m = int nmp.[1]
    let p = int nmp.[2]
    let one_cam = vectorRead fn 1
    let cam = arrayMapToMatrix (fun x -> one_cam)  (arrayRange 1 n)
    let one_x = vectorRead fn 2
    let x = arrayMapToMatrix (fun x -> one_x)  (arrayRange 1 m)
    let one_w = numberRead fn 3
    let w = arrayMap (fun x -> one_w)  (arrayRange 1 p)
    let one_feat = vectorRead fn 4
    let feat = arrayMapToMatrix (fun x -> one_feat)  (arrayRange 1 p)
    let obs = arrayMapToMatrix (fun x -> [| double ((int x) % n); double ((int x) % m) |] )  (arrayRange 0 (p - 1))
    let t = tic()
    let res = reproj_err cam x w obs feat
    toc(t)
    res

let inline logsumexp (arr: Vector) =
    let mx = arrayMax arr
    let semx = arraySum (arrayMap (fun x -> exp(x-mx)) arr)
    (log semx) + mx

let inline log_gamma_distrib (a: Number) (p: Number) =
  log (System.Math.Pow(System.Math.PI,(0.25*(p*(p-1.0))))) + 
    arraySum (arrayMap (fun j -> 
        MathNet.Numerics.SpecialFunctions.GammaLn (a + 0.5*(1. - (float j)))) 
      (arrayRange 1 (int p)))

let inline new_matrix_test (dum: Vector): Matrix = 
  let res = [| [| 0.0; 0.0; 0.0 |] |]
  res

let inline to_pose_params (theta: Vector) (n_bones: Index): Matrix =
  let row1 = theta.[0..2]
  let row2 = [| 1.0; 1.0; 1.0|]
  let row3 = theta.[3..5]
  let zeroRow = [| 0.0; 0.0; 0.0 |]
  let pose_params = [| row1; row2; row3; zeroRow; zeroRow |]
  let i1 = 5
  let finger1 = 
    [| [| theta.[i1]; theta.[i1+1]; 0.0 |] ; 
       [| theta.[i1+2]; 0.0; 0.0 |] ;
       [| theta.[i1+3]; 0.0; 0.0 |] ;
       [| 0.0; 0.0; 0.0 |] |]
  let i2 = i1 + 4
  let finger2 = 
    [| [| theta.[i2]; theta.[i2+1]; 0.0 |] ; 
       [| theta.[i2+2]; 0.0; 0.0 |] ;
       [| theta.[i2+3]; 0.0; 0.0 |] ;
       [| 0.0; 0.0; 0.0 |] |]
  let i3 = i2 + 4
  let finger3 = 
    [| [| theta.[i3]; theta.[i3+1]; 0.0 |] ; 
       [| theta.[i3+2]; 0.0; 0.0 |] ;
       [| theta.[i3+3]; 0.0; 0.0 |] ;
       [| 0.0; 0.0; 0.0 |] |]
  let i4 = i3 + 4
  let finger4 = 
    [| [| theta.[i4]; theta.[i4+1]; 0.0 |] ; 
       [| theta.[i4+2]; 0.0; 0.0 |] ;
       [| theta.[i4+3]; 0.0; 0.0 |] ;
       [| 0.0; 0.0; 0.0 |] |]
  let i5 = i4 + 4
  let finger5 = 
    [| [| theta.[i5]; theta.[i5+1]; 0.0 |] ; 
       [| theta.[i5+2]; 0.0; 0.0 |] ;
       [| theta.[i5+3]; 0.0; 0.0 |] ;
       [| 0.0; 0.0; 0.0 |] |]

  matrixConcat pose_params 
    (matrixConcat finger1 
      (matrixConcat finger2 
        (matrixConcat finger3 
          (matrixConcat finger4 finger5))))

let euler_angles_to_rotation_matrix (xzy: Vector): Matrix =
  let tx = xzy.[0]
  let ty = xzy.[2]
  let tz = xzy.[1]
  let Rx = [| [|1.; 0.; 0.|]; [|0.; cos(tx); -sin(tx)|]; [|0.; sin(tx); cos(tx)|] |]
  let Ry = [| [|cos(ty); 0.; sin(ty)|]; [|0.; 1.; 0.|]; [|-sin(ty); 0.; cos(ty)|] |]
  let Rz = [| [|cos(tz); -sin(tz); 0.|]; [|sin(tz); cos(tz); 0.|]; [|0.; 0.; 1.|] |]
  matrixMult Rz (matrixMult Ry Rx)

let matrixConcatCol (m1: Matrix) (m2: Matrix): Matrix = 
  let m1t = matrixTranspose m1
  let m2t = matrixTranspose m2
  matrixTranspose (matrixConcat m1t m2t)

let make_relative (pose_params: Vector) (base_relative: Matrix): Matrix =
  let R = euler_angles_to_rotation_matrix pose_params
  let T = 
    matrixConcat (matrixConcatCol R 
                    ([| [| 0. |]; [| 0. |]; [| 0. |] |])) 
                 ([| [| 0.; 0.; 0.; 1.0|] |])
  matrixPrint T
  matrixMult base_relative T

let test1 (dum: Vector) =
  let a = [| 1.0; 2.0; 3.0 |]
  let b = [| 5.0; 6.0; 7.0 |]
  arrayPrint a
  arrayPrint b
(*  arrayPrint (foo b) *)

  let c = cross a b
  arrayPrint c
  let d = mult_by_scalar c 15.0
  arrayPrint d
  let e = add_vec a b
  arrayPrint e
  let f = sub_vec a b
  arrayPrint f
  let g = add_vec3 a b c
  arrayPrint g
  let h = sqnorm a
  numberPrint h
  let i = dot_prod a b
  numberPrint i
  let j = radial_distort a b
  arrayPrint j
  let k = rodrigues_rotate_point a b
  arrayPrint k
  let l = k.[1..2]
  arrayPrint l
  let cam = [|0.; 2.; 4.; 6.; 8.; 10.; 12.; 14.; 16.; 18.; 20.|]
  let m = project cam j 
  arrayPrint m

  let mat1 = 
    [| [| 1.0; 2.0; 3.0; |];
       [| 4.0; 5.0; 6.0; |];
       [| 7.0; 8.0; 9.0; |] |]
  let n = matrixMult mat1 mat1
  matrixPrint n
  let o = matrixTranspose n
  matrixPrint o
  let p = matrixConcatCol mat1 mat1
  matrixPrint p
  let q = make_relative a mat1
  matrixPrint q
  ()