// HiFiCoordinateFrameUtil.cs

using UnityEngine;

namespace HiFi {

    /// 3D games in Unity use a left-handed coordinate system with:
    ///
    /// forward = +Zaxis
    /// up = +Yaxis
    ///
    /// whereas HiFi Spatial Audio uses a right-handed coordinate system with:
    ///
    /// forward = -Zaxis
    /// up = +Yaxis
    ///
    /// For HiFi Spatial Audio to be correct: all HiFiAudioAPIData must be transformed
    /// into the expected HiFi-frame.  The helper functions below are designed to make
    /// this process easier.
    ///

    public class HiFiCoordinateFrameUtil {

        static Matrix4x4 _unityToHiFi;
        static Matrix4x4 _hiFiToUnity;

        static Matrix4x4 MatrixFromQuaternion(Quaternion q) {
            // from https://www.euclideanspace.com/maths/geometry/rotations/conversions/quaternionToMatrix/hamourus.htm
            float sqw = q.w * q.w;
            float sqx = q.x * q.x;
            float sqy = q.y * q.y;
            float sqz = q.z * q.z;

            // rotation matrix is scaled by quaternion length squared
            // so we can avoid a sqrt operation by multiplying each matrix element by inverse square length
            float invs = 1.0f / (sqx + sqy + sqz + sqw);

            Vector3 a = new Vector3();
            Vector3 b = new Vector3();
            Vector3 c = new Vector3();
            a.x = ( sqx - sqy - sqz + sqw) * invs;
            b.y = (-sqx + sqy - sqz + sqw) * invs;
            c.z = (-sqx - sqy + sqz + sqw) * invs;

            float tmp1 = q.x * q.y;
            float tmp2 = q.z * q.w;
            b.x = 2.0f * (tmp1 + tmp2) * invs;
            a.y = 2.0f * (tmp1 - tmp2) * invs;

            tmp1 = q.x * q.z;
            tmp2 = q.y * q.w;
            c.x = 2.0f * (tmp1 - tmp2) * invs;
            a.z = 2.0f * (tmp1 + tmp2) * invs;

            tmp1 = q.y * q.z;
            tmp2 = q.x * q.w;
            c.y = 2.0f * (tmp1 + tmp2) * invs;
            b.z = 2.0f * (tmp1 - tmp2) * invs;

            Matrix4x4 m = Matrix4x4.identity;
            m.SetRow(0, a);
            m.SetRow(1, b);
            m.SetRow(2, c);
            return m;
        }

        //
        // return the rotation part of the Matrix3 in Quaternion form
        //
        static Quaternion QuaternionFromMatrix(Matrix4x4 m) {
            // from https://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/
            //
            // NOTE: this math will produce bogus (partially clipped) Quaternions
            // if the Matrix has non-unitary scale.
            //
            Quaternion q = new Quaternion();
            q.w = (float)System.Math.Sqrt(System.Math.Max(0.0, 1.0 + m[0,0] + m[1,1] + m[2,2] )) * 0.5f;
            q.x = (float)System.Math.Sqrt(System.Math.Max(0.0, 1.0 + m[0,0] - m[1,1] - m[2,2] )) * 0.5f;
            q.y = (float)System.Math.Sqrt(System.Math.Max(0.0, 1.0 - m[0,0] + m[1,1] - m[2,2] )) * 0.5f;
            q.z = (float)System.Math.Sqrt(System.Math.Max(0.0, 1.0 - m[0,0] - m[1,1] + m[2,2] )) * 0.5f;
            if (m[2,1] - m[1,2] < 0.0f) {
                q.x *= -1.0f;
            }
            if (m[0,2] - m[2,0] < 0.0f) {
                q.y *= -1.0f;
            }
            if (m[1,0] - m[0,1] < 0.0f) {
                q.z *= -1.0f;
            }
            return q;
        }

        public static void ComputeTransforms3D() {
            // Always call ComputeTransforms3D() or ComputeTransforms2D() before
            // using the other methods.  The transforms could be initialized by
            // magic but instead we show the logic of how it is done:
            //
            // We compute a transform from Unity-frame to a "canonical-frame"
            // which is a right-handed coordinate frame with the cardinal directions.
            //
            //     forward = xAxis
            //     up = yAxis
            //     right = zAxis
            //
            // This is matrix whose rows are the fwd, up, right directions:
            //
            //         | <- fwd -> |
            //     m = | <- up  -> |
            //         | <-right-> |
            //
            // This is true when the matrix operates from the LEFT on a hypothetical
            // columnar Vector4 on the RIGHT.  Each component of the hypothetical result
            // corresponds to the vector dot-product between: a row and the hypothetical vector.
            //
            // 3D games in Unity use a left-handed coordinate system with:
            //
            //     forward = +Zaxis
            //     up = +Yaxis
            //
            Vector3 fwd = Vector3.forward;
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, fwd); // swapped order because left-handed
            Matrix4x4 unityToCanonical = Matrix4x4.identity;
            unityToCanonical.SetRow(0, new Vector4(fwd.x, fwd.y, fwd.z));
            unityToCanonical.SetRow(1, new Vector4(up.x, up.y, up.z));
            unityToCanonical.SetRow(2, new Vector4(right.x, right.y, right.z));

            // Similarly for HiFi-frame to canonical-frame:
            fwd = new Vector3(0.0f, 0.0f, -1.0f);
            up = new Vector3(0.0f, 1.0f, 0.0f);
            right = Vector3.Cross(fwd, up);
            Matrix4x4 hiFiToCanonical = Matrix4x4.identity;
            hiFiToCanonical.SetRow(0, new Vector4(fwd.x, fwd.y, fwd.z));
            hiFiToCanonical.SetRow(1, new Vector4(up.x, up.y, up.z));
            hiFiToCanonical.SetRow(2, new Vector4(right.x, right.y, right.z));

            // The final transform is the product:
            //
            //     unityToHiFi = canonicalToHiFi * unityToCanonical
            //
            // where we invert hiFiToCanonical on the far left and the final matrix
            // operates from the LEFT on a hypothetical columnar Vector4 on the RIGHT.
            //
            _unityToHiFi = hiFiToCanonical.inverse * unityToCanonical;

            // We also cache the inverse transform for rotation transformations
            _hiFiToUnity = _unityToHiFi.inverse;
        }

        public static void ComputeTransforms2D() {
            // Similar to the ComputeTransforms3D() case:
            //
            // We compute a transform from Unity-frame to a "canonical-frame"
            // which is a right-handed coordinate frame with the cardinal directions.
            //
            //     forward = yAxis
            //     up = zAxis
            //     right = xAxis
            //
            // This is matrix whose rows are the fwd, up, right directions:
            //
            //         | <- fwd -> |
            //     m = | <- up  -> |
            //         | <-right-> |
            //
            // This is true when the matrix operates from the LEFT on a hypothetical
            // columnar Vector4 on the RIGHT.  Each component of the hypothetical result
            // corresponds to the vector dot-product between: a row and the hypothetical vector.
            //
            // The Unity2D case uses different "forward" and "up" directions than the 3D case:
            //
            //     forward = +Yaxis
            //     up = -Zaxis
            //
            Vector3 fwd = new Vector3(0.0f, 1.0f, 0.0f);
            Vector3 up = new Vector3(0.0f, 0.0f, -1.0f);
            Vector3 right = Vector3.Cross(up, fwd); // swapped order because left-handed
            Matrix4x4 unityToCanonical = Matrix4x4.identity;
            unityToCanonical.SetRow(0, new Vector4(fwd.x, fwd.y, fwd.z));
            unityToCanonical.SetRow(1, new Vector4(up.x, up.y, up.z));
            unityToCanonical.SetRow(2, new Vector4(right.x, right.y, right.z));

            // Similarly for HiFi-frame to canonical-frame:
            fwd = new Vector3(0.0f, 0.0f, -1.0f);
            up = new Vector3(0.0f, 1.0f, 0.0f);
            right = Vector3.Cross(fwd, up);
            Matrix4x4 hiFiToCanonical = Matrix4x4.identity;
            hiFiToCanonical.SetRow(0, new Vector4(fwd.x, fwd.y, fwd.z));
            hiFiToCanonical.SetRow(1, new Vector4(up.x, up.y, up.z));
            hiFiToCanonical.SetRow(2, new Vector4(right.x, right.y, right.z));

            // The final transform is the product:
            //
            //     unityToHiFi = canonicalToHiFi * unityToCanonical
            //
            // where we invert hiFiToCanonical on the far left and the final matrix
            // operates from the LEFT on a hypothetical columnar Vector4 on the RIGHT.
            //
            _unityToHiFi = hiFiToCanonical.inverse * unityToCanonical;

            // We also cache the inverse transform for rotation transformations
            _hiFiToUnity = _unityToHiFi.inverse;
        }

        public static Vector4 UnityPositionToHiFi(Vector3 v) {
            return _unityToHiFi.MultiplyVector(v);
        }

        public static Vector4 HiFiPositionToUnity(Vector3 v) {
            return _hiFiToUnity.MultiplyVector(v);
        }

        public static Quaternion UnityOrientationToHiFi(Quaternion q) {
            // To compute the HiFi-frame rotation we build a matrix which operates
            // from the LEFT on a hypothetical vector on the RIGHT
            // and which first transforms into Unity-frame where it applies
            // the rotation and then transforms back to HiFi-frame.
            // Finally we compute the equivalent Quaternion of the whole operation.
            //
            // DANGER: this calculation will produce bogus (partially clipped) rotations
            // if the Quaternion argument is not normalized.
            //
            return QuaternionFromMatrix(_unityToHiFi * MatrixFromQuaternion(q) * _hiFiToUnity);
        }

        public static Quaternion HiFiOrientationToUnity(Quaternion q) {
            return QuaternionFromMatrix(_hiFiToUnity * MatrixFromQuaternion(q) * _unityToHiFi);
        }
    }
} // namespace
