#ifndef BLADEHOLD_BAKED_CROWD_SKINNING_INCLUDED
#define BLADEHOLD_BAKED_CROWD_SKINNING_INCLUDED

// GPU skinning from a baked bone-matrix texture (see BakedCrowdAnimationSO for the layout): one
// row per frame, three texels per bone holding the top three rows of that bone's skin matrix.
// The mesh carries its four bone indices in UV4 and weights in UV5 (written by BakedCrowdBaker),
// so it renders as a plain instanced mesh with no SkinnedMeshRenderer.
//
// Per instance, _CrowdFrame = (frame A, frame B, weight of B, unused): B is the clip being faded
// out, so a crossfade is a linear blend of the two poses' skin matrices.

TEXTURE2D(_CrowdBoneTex);

UNITY_INSTANCING_BUFFER_START(CrowdProps)
    UNITY_DEFINE_INSTANCED_PROP(float4, _CrowdFrame)
UNITY_INSTANCING_BUFFER_END(CrowdProps)

float3x4 CrowdLoadBone(uint frame, uint bone)
{
    uint x = bone * 3;
    float4 r0 = LOAD_TEXTURE2D_LOD(_CrowdBoneTex, uint2(x, frame), 0);
    float4 r1 = LOAD_TEXTURE2D_LOD(_CrowdBoneTex, uint2(x + 1, frame), 0);
    float4 r2 = LOAD_TEXTURE2D_LOD(_CrowdBoneTex, uint2(x + 2, frame), 0);
    return float3x4(r0, r1, r2);
}

float3x4 CrowdBone(float4 crowdFrame, uint bone)
{
    float3x4 m = CrowdLoadBone((uint)crowdFrame.x, bone);
    if (crowdFrame.z > 0.001)
    {
        m = lerp(m, CrowdLoadBone((uint)crowdFrame.y, bone), crowdFrame.z);
    }
    return m;
}

// Call after UNITY_SETUP_INSTANCE_ID.
void CrowdSkin(float3 positionOS, float3 normalOS, float3 tangentOS, float4 boneIndices, float4 boneWeights,
               out float3 skinnedPosition, out float3 skinnedNormal, out float3 skinnedTangent)
{
    float4 crowdFrame = UNITY_ACCESS_INSTANCED_PROP(CrowdProps, _CrowdFrame);
    uint4 idx = (uint4)(boneIndices + 0.5);
    float3x4 skin = CrowdBone(crowdFrame, idx.x) * boneWeights.x;
    if (boneWeights.y > 0.0) skin += CrowdBone(crowdFrame, idx.y) * boneWeights.y;
    if (boneWeights.z > 0.0) skin += CrowdBone(crowdFrame, idx.z) * boneWeights.z;
    if (boneWeights.w > 0.0) skin += CrowdBone(crowdFrame, idx.w) * boneWeights.w;

    skinnedPosition = mul(skin, float4(positionOS, 1.0));
    skinnedNormal = normalize(mul((float3x3)skin, normalOS));
    skinnedTangent = mul((float3x3)skin, tangentOS);
}

#endif
