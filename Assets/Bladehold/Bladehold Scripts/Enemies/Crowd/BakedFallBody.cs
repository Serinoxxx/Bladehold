using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     The one physics body behind a baked ragdoll fall (<see cref="BakedCrowdAgent.PlayBakedFall" />):
///     a rotation-locked sphere Rigidbody that takes the real launch velocity and carries the
///     goblin's root, while the baked clip does the tumble. It sits on the Ragdoll layer, so it lands
///     on and bounces off the world like a real ragdoll but passes through goblins and the player.
///     Pooled: created on demand under one scene object, deactivated when released, and gone with the
///     scene like the other on-demand singletons.
/// </summary>
public class BakedFallBody : MonoBehaviour
{
    private const string LayerName = "Ragdoll";
    private const float GroundedGrace = 0.1f;
    private const float MinGroundNormalY = 0.5f;

    private static readonly Stack<BakedFallBody> pool = new Stack<BakedFallBody>();
    private static Transform poolRoot;
    private static PhysicsMaterial material;
    private static int layer = -1;
    private static int collisionMask;

    private Rigidbody body;
    private SphereCollider sphere;
    private float lastGroundedTime = Mathf.NegativeInfinity;

    /// <summary>
    ///     Touching walkable ground (a contact facing up) within the last moment, or asleep: a resting
    ///     body gets no more collision callbacks.
    /// </summary>
    public bool Grounded => body.IsSleeping() || Time.time - lastGroundedTime < GroundedGrace;

    public float Speed => body.linearVelocity.magnitude;

    /// <summary>Layers the fall body collides with (the Ragdoll layer's row of the collision matrix): where baked blood lands.</summary>
    public static int CollisionMask
    {
        get
        {
            ResolveLayer();
            return collisionMask;
        }
    }

    /// <summary>Takes a body from the pool, centred at <paramref name="position" /> and moving at <paramref name="velocity" />.</summary>
    public static BakedFallBody Acquire(Vector3 position, Vector3 velocity, float radius, float friction)
    {
        if (poolRoot == null)
        {
            // The scene that owned the pool unloaded; its bodies went with it.
            pool.Clear();
            poolRoot = new GameObject("BakedFallBodies").transform;
        }
        ResolveLayer();
        if (material == null)
        {
            material = new PhysicsMaterial("Baked Fall Body")
            {
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
        }
        material.dynamicFriction = friction;
        material.staticFriction = friction;

        BakedFallBody fallBody = null;
        while (pool.Count > 0 && fallBody == null)
        {
            fallBody = pool.Pop();
        }
        if (fallBody == null)
        {
            fallBody = Create();
        }

        fallBody.sphere.radius = radius;
        fallBody.lastGroundedTime = Mathf.NegativeInfinity;
        fallBody.transform.SetPositionAndRotation(position, Quaternion.identity);
        fallBody.gameObject.SetActive(true);
        fallBody.body.position = position;
        fallBody.body.linearVelocity = velocity;
        fallBody.body.angularVelocity = Vector3.zero;
        return fallBody;
    }

    private static BakedFallBody Create()
    {
        var go = new GameObject("BakedFallBody");
        go.SetActive(false);
        go.layer = layer;
        go.transform.SetParent(poolRoot, false);
        var fallBody = go.AddComponent<BakedFallBody>();
        fallBody.sphere = go.AddComponent<SphereCollider>();
        fallBody.sphere.sharedMaterial = material;
        fallBody.body = go.AddComponent<Rigidbody>();
        fallBody.body.mass = 1f;
        fallBody.body.linearDamping = 0f;
        fallBody.body.constraints = RigidbodyConstraints.FreezeRotation;
        fallBody.body.interpolation = RigidbodyInterpolation.Interpolate;
        fallBody.body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        return fallBody;
    }

    private static void ResolveLayer()
    {
        if (layer >= 0) return;
        layer = LayerMask.NameToLayer(LayerName);
        if (layer < 0)
        {
            Debug.LogError($"BakedFallBody: no '{LayerName}' layer; baked falls will collide with everything on Default.");
            layer = 0;
        }
        collisionMask = 0;
        for (int i = 0; i < 32; i++)
        {
            if (!Physics.GetIgnoreLayerCollision(layer, i)) collisionMask |= 1 << i;
        }
    }

    public void AddVelocity(Vector3 velocityChange)
    {
        body.linearVelocity += velocityChange;
    }

    public void Release()
    {
        gameObject.SetActive(false);
        pool.Push(this);
    }

    private void OnCollisionEnter(Collision collision) => CheckGround(collision);

    private void OnCollisionStay(Collision collision) => CheckGround(collision);

    private void CheckGround(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > MinGroundNormalY)
            {
                lastGroundedTime = Time.time;
                return;
            }
        }
    }
}
