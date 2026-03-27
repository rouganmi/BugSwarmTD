using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [SerializeField] private List<Pool> pools = new List<Pool>();
    private Dictionary<string, Pool> poolDict = new Dictionary<string, Pool>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        foreach (var pool in pools)
        {
            pool.Initialize(transform);
            poolDict.Add(pool.key, pool);
        }
    }

    public GameObject Spawn(string key, Vector3 position, Quaternion rotation)
    {
        if (!poolDict.ContainsKey(key))
        {
            Debug.LogError($"对象池不存在: {key}");
            return null;
        }

        GameObject obj = poolDict[key].Get();
        if (obj == null)
        {
            Debug.LogError($"对象池生成失败: {key}，请检查该池的 Prefab 引用是否为空。");
            return null;
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        return obj;
    }

    public void Despawn(GameObject obj)
    {
        PoolObject poolObject = obj.GetComponent<PoolObject>();
        if (poolObject == null)
        {
            Debug.LogError("回收失败，物体没有 PoolObject 组件");
            Destroy(obj);
            return;
        }

        if (!poolDict.ContainsKey(poolObject.poolKey))
        {
            Debug.LogError($"未找到对象池: {poolObject.poolKey}");
            Destroy(obj);
            return;
        }

        poolDict[poolObject.poolKey].Return(obj);
    }
}
