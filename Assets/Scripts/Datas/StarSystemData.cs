using UnityEngine;

[CreateAssetMenu(fileName = "New Star System", menuName = "Space/Star System Data")]
public class StarSystemData : ScriptableObject
{
    public string systemName;      // 예: "Alpha Centauri"
    public Vector3 galacticCoords; // 은하계 내의 절대 좌표
    public Color starColor;        // 워프 포인트나 기즈모에 표시될 색상
    // public List<PlanetData> planets; // 나중에 행성 데이터도 추가 가능
}