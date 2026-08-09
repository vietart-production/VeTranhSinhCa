using System.Collections.Generic;
using UnityEngine;

public class BackgroundFishSpawner : MonoBehaviour
{
    [Header("Khu vực xuất hiện")]
    public BoxCollider2D spawnBounds;

    [Header("Đàn cá")]
    public List<GameObject> fishPrefabs;
    public int numberOfFishToSpawn = 10;

    void Start()
    {
        if (spawnBounds == null)
        {
            Debug.LogError("[BackgroundFishSpawner] Vui lòng kéo thả BoxCollider2D vào ô Spawn Bounds!");
            return;
        }

        if (fishPrefabs == null || fishPrefabs.Count == 0)
        {
            Debug.LogError("[BackgroundFishSpawner] Vui lòng thêm ít nhất 1 Prefab cá vào danh sách Fish Prefabs!");
            return;
        }

        SpawnSchoolOfFish();
    }

    void SpawnSchoolOfFish()
    {
        Bounds bounds = spawnBounds.bounds;
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minY = bounds.min.y;
        float maxY = bounds.max.y;

        for (int i = 0; i < numberOfFishToSpawn; i++)
        {
            // Chọn ngẫu nhiên 1 loại cá trong danh sách
            GameObject randomPrefab = fishPrefabs[Random.Range(0, fishPrefabs.Count)];

            if (randomPrefab == null) continue;

            // Tính toán vị trí ngẫu nhiên trong vùng Bounds
            float randomX = Random.Range(minX, maxX);
            float randomY = Random.Range(minY, maxY);
            Vector3 spawnPos = new Vector3(randomX, randomY, randomPrefab.transform.position.z);

            // Sinh ra cá
            GameObject newFish = Instantiate(randomPrefab, spawnPos, Quaternion.identity, transform);
            newFish.name = "BackgroundFish_" + i;

            // Nếu muốn đàn cá background bơi vĩnh viễn không bị biến mất:
            // Cố gắng tìm DOTweenFishAnim và tắt tính năng giới hạn thời gian (lifetime)
            DOTweenFishAnim animScript = newFish.GetComponent<DOTweenFishAnim>();
            if (animScript != null)
            {
                // Cho lifetime một con số cực kỳ lớn để nó không bao giờ chết
                animScript.lifetime = 999999f; 
            }
        }
    }
}
