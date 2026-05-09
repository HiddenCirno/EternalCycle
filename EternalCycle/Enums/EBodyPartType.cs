namespace VulcanCore;

[Flags]
public enum EBodyPartType
{
    None = 0,
    Head = 1 << 0,     //1
    Chest = 1 << 1,    //2
    Stomach = 1 << 2,  //4
    LeftArm = 1 << 3,  //8
    RightArm = 1 << 4, //16
    LeftLeg = 1 << 5,  //32
    RightLeg = 1 << 6  //64
}