using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public static class MatrixExtensions
{
    public static float DeterminantSubMatrix(this Matrix4x4 m)
    {
        return m.m00* m.m11 * m.m22 + m.m01 * m.m12 * m.m20 + m.m02 * m.m10 * m.m21 -
               m.m00 * m.m12 * m.m21 - m.m01 * m.m10 * m.m22 - m.m02 * m.m11 * m.m20;
    }

    public static Matrix4x4 AdjugateSubMatrix(this Matrix4x4 m)
    {
        Matrix4x4 output = new Matrix4x4();

        output.m00 = m.m11 * m.m22 - m.m12 * m.m21;
        output.m01 = -(m.m01 * m.m22 - m.m02 * m.m21);
        output.m02 = m.m01 * m.m12 - m.m02 * m.m11;

        output.m10 = -(m.m10 * m.m22 - m.m12 * m.m20);
        output.m11 = m.m00 * m.m22 - m.m02 * m.m20;
        output.m12 = -(m.m00 * m.m12 - m.m02 * m.m10);

        output.m20 = m.m10 * m.m21 - m.m11 * m.m20;
        output.m21 = -(m.m00 * m.m21 - m.m01 * m.m20);
        output.m22 = m.m00 * m.m11 - m.m01 * m.m10;

        output.m30 = m.m30;
        output.m31 = m.m31;
        output.m32 = m.m32;
        output.m33 = m.m33;

        return output;
    }

    public static Matrix4x4 InverseSubMatrix(this Matrix4x4 m)
    {
        float det = m.DeterminantSubMatrix();

        if (det == 0)
        {
            throw new System.ArgumentException("Cannot calculate inverse of the matrix since it is singular.");
        }

        float detInv = 1 / det;
        Matrix4x4 output = m.AdjugateSubMatrix();

        output.m00 *= detInv;
        output.m01 *= detInv;
        output.m02 *= detInv;

        output.m10 *= detInv;
        output.m11 *= detInv;
        output.m12 *= detInv;

        output.m20 *= detInv;
        output.m21 *= detInv;
        output.m22 *= detInv;

        output.m30 = m.m30;
        output.m31 = m.m31;
        output.m32 = m.m32;
        output.m33 = m.m33;

        return output;
    }
}
