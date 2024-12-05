[System.Serializable]
public class GameSceneData
{
    public string sceneName;
    public int buildIndex;      
}

public static class GameSceneDatabase
{
    public const string SCENE_PATH_PREFIX = "Assets/Scenes/GameScenes/";

    public static GameSceneData[] Scenes = new GameSceneData[]
    {
        new GameSceneData
        {
            sceneName = "Classroom",
            buildIndex = 1,
        },
    };
}