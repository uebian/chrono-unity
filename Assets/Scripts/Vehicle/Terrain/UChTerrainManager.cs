using UnityEngine;



public abstract class UChTerrainManager : MonoBehaviour,IUChTerrainManager {
    public ChTerrain chronoTerrain {get; set;}
}

public interface IUChTerrainManager {
    ChTerrain chronoTerrain {get; set;}

}