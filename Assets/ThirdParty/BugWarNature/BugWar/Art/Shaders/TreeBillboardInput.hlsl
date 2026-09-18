#ifndef BUGWAR_TREE_BILLBOARD_INPUT_INCLUDED
#define BUGWAR_TREE_BILLBOARD_INPUT_INCLUDED

// Shared by every pass of TreeBillboard.shader. Only _Cutoff, _LightWrap and _AmbientBoost are
// per-material — the two atlas textures live outside the block per the usual SRP batcher rule (a
// texture in UnityPerMaterial breaks batching), and _TreeIndex lives in its own instancing buffer
// below because, unlike the rest, it differs per tree while the material stays shared.

CBUFFER_START(UnityPerMaterial)
    float _Cutoff;
    float _LightWrap;
    float _AmbientBoost;
CBUFFER_END

TEXTURE2D(_TreeAtlas);
SAMPLER(sampler_TreeAtlas);
TEXTURE2D(_TreeAtlasNormal);
SAMPLER(sampler_TreeAtlasNormal);

// _TreeIndex says which of the atlas's tree rows (0 = A, 1 = B, 2 = C — SandboxTreeImposters
// bakes no more than that) this instance samples. Every billboard in the ring shares one material
// so GPU instancing can batch them into a handful of draw calls; routing the one thing that
// varies per tree through the instancing buffer, rather than an ordinary per-renderer material
// override, is what keeps that batching intact — an MPB override of a plain CBUFFER value forces
// its own draw call per distinct value, same as no instancing at all.
UNITY_INSTANCING_BUFFER_START(TreeBillboardPerInstance)
    UNITY_DEFINE_INSTANCED_PROP(float, _TreeIndex)
UNITY_INSTANCING_BUFFER_END(TreeBillboardPerInstance)

// The atlas grid SandboxTreeImposters.Build() bakes: eight yaw angles across the columns, four
// row slots down (three trees, one spare) so both axes divide 2048 evenly. Fixed by the baker,
// not a material property — every billboard reads the same grid, so there is nothing to tune.
#define TREE_ATLAS_COLUMNS 8.0
#define TREE_ATLAS_ROWS    4.0

struct TreeBillboardVertex
{
    float3 positionWS;
    float3 normalWS;
    float2 uv;
    float3 cardRightWS;
    float3 cardForwardWS;
};

/// Turns one quad-local vertex (positionOS.xy holds the width/height offset from the tree's own
/// base pivot, in metres at the prefab's reference scale; z is unused — the quad is flat) into
/// its world position, its billboard-facing basis, and the atlas UV for whichever of the eight
/// baked angles best matches how the camera currently sees this particular tree. Every pass
/// (forward, shadow, depth) shares this so the card's shape, silhouette and atlas cell agree with
/// each other everywhere it is drawn.
///
/// Call it after UNITY_SETUP_INSTANCE_ID — it reads unity_ObjectToWorld and the per-instance
/// _TreeIndex, both of which need the instance ID set up first.
TreeBillboardVertex ComputeTreeBillboardVertex(float3 positionOS, float2 uv)
{
    TreeBillboardVertex result = (TreeBillboardVertex)0;

    float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));

    // The card's own yaw: the object's local +Z axis in world space. Every spawned tree only
    // ever rotates about Y — SandboxTreeRing never tilts one or leans it to the ground's slope —
    // so this is exactly the yaw the ring scattered it at.
    float3 objectForwardWS = normalize(TransformObjectToWorldDir(float3(0, 0, 1)));
    float objectYaw = atan2(objectForwardWS.x, objectForwardWS.z);

    float3 toCameraWS = _WorldSpaceCameraPos - pivotWS;
    toCameraWS.y = 0.0;
    float3 flatToCamera = normalize(toCameraWS);
    float cameraYaw = atan2(flatToCamera.x, flatToCamera.z);

    // The nearest of the eight baked angles to the camera's bearing measured in the tree's own
    // local frame. SandboxTreeImposters bakes column i by circling the camera to world yaw
    // i * 45 deg around an unrotated tree, which is the same angle measured the same way — see
    // its class summary. 6.2831853 is 2*PI (one full turn); Chitin.shader uses the same literal
    // for the same reason: no shader-wide constant is worth a new include just for this.
    float turns = frac((cameraYaw - objectYaw) / 6.2831853);
    float column = fmod(floor(turns * TREE_ATLAS_COLUMNS + 0.5), TREE_ATLAS_COLUMNS);
    float row = UNITY_ACCESS_INSTANCED_PROP(TreeBillboardPerInstance, _TreeIndex);

    result.uv = float2((column + uv.x) / TREE_ATLAS_COLUMNS, (row + uv.y) / TREE_ATLAS_ROWS);

    // Y-axis-only billboard: right is perpendicular to the flattened view direction and up stays
    // world-up, so the card turns to face the camera in yaw only. A fully camera-facing quad
    // would tilt back as the camera pitches up and lift the trunk base off the ground.
    float3 right = normalize(cross(float3(0, 1, 0), flatToCamera));

    // The instance's own uniform scale — SandboxTreeRing only ever spawns trees with
    // Vector3.one * scale — read off the object matrix so the card grows and shrinks with the
    // mesh LOD it replaces instead of staying fixed at the prefab's reference size.
    float scale = length(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));

    result.positionWS = pivotWS + right * (positionOS.x * scale) + float3(0, 1, 0) * (positionOS.y * scale);
    result.cardRightWS = right;
    result.cardForwardWS = flatToCamera;

    // No true per-angle normal is baked — see TreeBillboard.shader's file header and
    // SandboxTreeImposters.WriteFlatNormalAtlas. The card's own face direction is the flat-normal
    // fallback the task allows, and using it here is what keeps that fallback and the atlas's
    // actual (uniformly flat) content agreeing with each other rather than being two independent
    // guesses.
    result.normalWS = flatToCamera;
    return result;
}

#endif
