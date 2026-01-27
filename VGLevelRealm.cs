using Realms;

namespace LessRam;

public partial class VGLevelRealm : IRealmObject
{
    [PrimaryKey]
    public string LevelId {get; set;}
    public string ImagePath {get; set;}
    public string AudioPath {get; set;}
    public string LevelPath {get; set;}
}