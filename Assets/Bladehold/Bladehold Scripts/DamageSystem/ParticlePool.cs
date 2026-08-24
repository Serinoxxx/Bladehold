using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Lightweight static pool for ParticleSystem effects to avoid runtime Instantiate/Destroy hitches.
/// </summary>
public static class ParticlePool
{
    private static readonly Dictionary<ParticleSystem, Queue<ParticleSystem>> pools = new Dictionary<ParticleSystem, Queue<ParticleSystem>>();
    private static Transform poolRoot;

    private static void EnsureRoot()
    {
        if (poolRoot == null)
        {
            GameObject go = GameObject.Find("ParticleEffectsPool");
            if (go == null)
            {
                go = new GameObject("ParticleEffectsPool");
                Object.DontDestroyOnLoad(go);
            }
            poolRoot = go.transform;
        }
    }

    public static ParticleSystem Get(ParticleSystem prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        EnsureRoot();

        if (!pools.TryGetValue(prefab, out Queue<ParticleSystem> pool))
        {
            pool = new Queue<ParticleSystem>();
            pools[prefab] = pool;
        }

        ParticleSystem instance = null;
        while (pool.Count > 0 && instance == null)
        {
            instance = pool.Dequeue();
        }

        if (instance == null)
        {
            instance = Object.Instantiate(prefab, position, rotation, poolRoot);
        }
        else
        {
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.gameObject.SetActive(true);
        }

        return instance;
    }

    public static void Release(ParticleSystem prefab, ParticleSystem instance, float delay = 0f)
    {
        if (instance == null || prefab == null) return;
        if (delay <= 0f)
        {
            ReturnToPool(prefab, instance);
        }
        else
        {
            EnsureRoot();
            if (poolRoot != null)
            {
                var runner = poolRoot.GetComponent<PoolCoroutineRunner>();
                if (runner == null) runner = poolRoot.gameObject.AddComponent<PoolCoroutineRunner>();
                runner.StartCoroutine(DelayedRelease(prefab, instance, delay));
            }
            else
            {
                ReturnToPool(prefab, instance);
            }
        }
    }

    private static IEnumerator DelayedRelease(ParticleSystem prefab, ParticleSystem instance, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool(prefab, instance);
    }

    private static void ReturnToPool(ParticleSystem prefab, ParticleSystem instance)
    {
        if (instance == null || prefab == null) return;
        instance.gameObject.SetActive(false);
        if (!pools.TryGetValue(prefab, out Queue<ParticleSystem> pool))
        {
            pool = new Queue<ParticleSystem>();
            pools[prefab] = pool;
        }
        pool.Enqueue(instance);
    }
}

public class PoolCoroutineRunner : MonoBehaviour { }
