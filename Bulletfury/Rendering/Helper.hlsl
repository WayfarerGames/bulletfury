#if UNITY_ANY_INSTANCING_ENABLED

StructuredBuffer<float4> _InstanceColorBuffer;
StructuredBuffer<float> _InstanceTexBuffer;
uint _InstanceIDOffset;

void GetInstanceColor_float(out float4 color)
{
    color = _InstanceColorBuffer[unity_InstanceID + _InstanceIDOffset];
}

void GetInstanceTex_float (out float tex)
{
    tex = _InstanceTexBuffer[unity_InstanceID + _InstanceIDOffset];
}

#else

void GetInstanceColor_float(out float4 color)
{
    color = float4(1, 0, 0, 1);
}

void GetInstanceTex_float (out float tex)
{
    tex = 0;
}

#endif

void GetAnimatedFrameUV_float (UnityTexture2D tex, float _Rows, float _Cols, float _Frame, float time, float2 uv, out float4 col)
{
    int frames = _Rows * _Cols;
    float frame = fmod(time / _Frame, frames);
    int current = floor(frame);
    float dx = 1.0 / _Cols;
    float dy = 1.0 / _Rows;
    float cellX = fmod(current, _Cols);

    float2 newUV = float2(
    cellX * dx + (1.0f - uv.x) * dx, // Corrected mirroring
        1.0 - ((uv.y * dy) + floor(current / _Cols) * dy)
      );

    col = tex2D(tex, newUV);
}