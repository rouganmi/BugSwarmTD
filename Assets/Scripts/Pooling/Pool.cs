using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Pool
{
    public string key;
    public GameObject prefab;
    public int initialSize = 10;

    private Queue<GameObject> objects = new Queue<GameObject>();
    private Transform parent;

    public void Initialize(Transform root)
    {
        if (prefab == null)
        {
            Debug.LogError($"[Pool] Initialize failed: pool '{key}' prefab is null. Please assign prefab in PoolManager.");
            return;
        }

        parent = new GameObject($"{key}_Pool").transform;
        parent.SetParent(root);

        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = Object.Instantiate(prefab, parent);
            obj.SetActive(false);

            PoolObject poolObject = obj.GetComponent<PoolObject>();
            if (poolObject == null)
                poolObject = obj.AddComponent<PoolObject>();

            poolObject.poolKey = key;
            objects.Enqueue(obj);
        }
    }

    public GameObject Get()
    {
        if (prefab == null)
        {
            Debug.LogError($"[Pool] Spawn failed: pool '{key}' prefab is null.");
            return null;
        }

        GameObject obj;

        if (objects.Count > 0)
        {
            obj = objects.Dequeue();
        }
        else
        {
            obj = Object.Instantiate(prefab, parent);

            PoolObject poolObject = obj.GetComponent<PoolObject>();
            if (poolObject == null)
                poolObject = obj.AddComponent<PoolObject>();

            poolObject.poolKey = key;
        }

        if (obj == null)
        {
            Debug.LogError($"[Pool] Spawn failed: pool '{key}' created null object.");
            return null;
        }

        obj.SetActive(true);

        IPoolable poolable = obj.GetComponent<IPoolable>();
        poolable?.OnSpawn();

        return obj;
    }

    public void Return(GameObject obj)
    {
        obj.SetActive(false);

        IPoolable poolable = obj.GetComponent<IPoolable>();
        poolable?.OnDespawn();

        objects.Enqueue(obj);
    }
}
