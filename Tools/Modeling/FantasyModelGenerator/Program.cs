using System.Globalization;
using System.Numerics;
using System.Text;

internal static class Program
{
    private static void Main(string[] args)
    {
        string projectRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : FindProjectRoot();
        string output = Path.Combine(projectRoot, "Assets", "_Project", "Art", "Models", "Generated");
        Directory.CreateDirectory(output);
        BuildLucia(output);
        BuildCharlotte(output);
        BuildZephyr(output);
        BuildPoko(output);
        Console.WriteLine("PRIDE_COURT_FANTASY_MODELS_GENERATED");
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assets")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Unity project root was not found.");
    }

    private static void BuildLucia(string output)
    {
        const string baseName = "Lucia_Fairy_Model_v1";
        ObjWriter model = new ObjWriter(baseName + ".mtl");
        AddSharedHumanBody(model,
            torso: "Lucia_Green", accent: "Lucia_Magenta", skin: "Lucia_Skin", hair: "Lucia_Purple",
            shoulderX: 0.43f, heightOffset: 0f);

        model.AddEllipsoid("Left_Wing", new Vector3(-0.42f, 1.78f, -0.22f), new Vector3(0.31f, 0.63f, 0.07f), "Lucia_Wing");
        model.AddEllipsoid("Right_Wing", new Vector3(0.42f, 1.78f, -0.22f), new Vector3(0.31f, 0.63f, 0.07f), "Lucia_Wing");
        model.AddBox("Left_Ear", new Vector3(-0.36f, 2.25f, 0f), new Vector3(0.28f, 0.12f, 0.13f), new Vector3(0f, 0f, 18f), "Lucia_Skin");
        model.AddBox("Right_Ear", new Vector3(0.36f, 2.25f, 0f), new Vector3(0.28f, 0.12f, 0.13f), new Vector3(0f, 0f, -18f), "Lucia_Skin");
        AddRacket(model, "Lucia_Magenta", "Lucia_Green", 1.02f, 0.56f);

        model.Write(Path.Combine(output, baseName + ".obj"));
        WriteMaterials(Path.Combine(output, baseName + ".mtl"), new Dictionary<string, Vector3>
        {
            ["Lucia_Green"] = new(0.04f, 0.52f, 0.30f),
            ["Lucia_Magenta"] = new(0.88f, 0.06f, 0.48f),
            ["Lucia_Skin"] = new(0.72f, 0.42f, 0.32f),
            ["Lucia_Purple"] = new(0.25f, 0.08f, 0.38f),
            ["Lucia_Wing"] = new(0.22f, 0.88f, 0.62f),
            ["Eye_Cyan"] = new(0.08f, 0.9f, 1f),
            ["Paper"] = new(0.92f, 0.95f, 0.9f),
            ["Ink"] = new(0.015f, 0.025f, 0.075f)
        });
    }

    private static void BuildCharlotte(string output)
    {
        const string baseName = "Charlotte_Dragon_Model_v1";
        ObjWriter model = new ObjWriter(baseName + ".mtl");
        AddSharedHumanBody(model,
            torso: "Charlotte_Navy", accent: "Charlotte_Crimson", skin: "Charlotte_Skin", hair: "Charlotte_Red",
            shoulderX: 0.46f, heightOffset: 0.04f);

        model.AddCylinder("Left_Horn", new Vector3(-0.17f, 2.54f, -0.02f), new Vector3(-0.27f, 2.82f, -0.08f), 0.07f, "Charlotte_Gold");
        model.AddCylinder("Right_Horn", new Vector3(0.17f, 2.54f, -0.02f), new Vector3(0.27f, 2.82f, -0.08f), 0.07f, "Charlotte_Gold");
        model.AddEllipsoid("Hair_Tuft_1", new Vector3(0f, 2.45f, -0.34f), new Vector3(0.24f, 0.52f, 0.18f), "Charlotte_Red");
        model.AddBox("L_Shoulder_Armor", new Vector3(-0.48f, 1.95f, 0f), new Vector3(0.3f, 0.22f, 0.38f), new Vector3(0f, 0f, -12f), "Charlotte_Gold");
        model.AddBox("R_Shoulder_Armor", new Vector3(0.48f, 1.95f, 0f), new Vector3(0.3f, 0.22f, 0.38f), new Vector3(0f, 0f, 12f), "Charlotte_Gold");

        Vector3[] tailPoints =
        {
            new(0f, 1.32f, -0.16f), new(0.18f, 1.10f, -0.48f), new(0.36f, 0.86f, -0.72f),
            new(0.5f, 0.58f, -0.83f), new(0.55f, 0.32f, -0.7f)
        };
        for (int i = 0; i < 4; i++)
            model.AddCylinder("Tail_" + i, tailPoints[i], tailPoints[i + 1], 0.15f - i * 0.022f,
                i % 2 == 0 ? "Charlotte_Crimson" : "Charlotte_Gold");
        AddRacket(model, "Charlotte_Crimson", "Charlotte_Gold", 1.04f, 0.58f);

        model.Write(Path.Combine(output, baseName + ".obj"));
        WriteMaterials(Path.Combine(output, baseName + ".mtl"), new Dictionary<string, Vector3>
        {
            ["Charlotte_Navy"] = new(0.025f, 0.06f, 0.18f),
            ["Charlotte_Crimson"] = new(0.62f, 0.025f, 0.07f),
            ["Charlotte_Skin"] = new(0.74f, 0.46f, 0.36f),
            ["Charlotte_Red"] = new(0.42f, 0.025f, 0.06f),
            ["Charlotte_Gold"] = new(0.92f, 0.57f, 0.08f),
            ["Eye_Cyan"] = new(0.08f, 0.9f, 1f),
            ["Paper"] = new(0.92f, 0.95f, 0.9f),
            ["Ink"] = new(0.015f, 0.025f, 0.075f)
        });
    }

    private static void BuildZephyr(string output)
    {
        const string baseName = "Zephyr_Jet_Model_v1";
        ObjWriter model = new ObjWriter(baseName + ".mtl");

        model.AddBox("Pelvis", new Vector3(0f, 1.32f, 0f), new Vector3(0.7f, 0.34f, 0.42f), Vector3.Zero, "Zephyr_Gunmetal");
        model.AddBox("Torso", new Vector3(0f, 1.83f, 0f), new Vector3(0.78f, 0.78f, 0.44f), Vector3.Zero, "Zephyr_White");
        model.AddBox("Chest_Accent", new Vector3(0f, 1.9f, 0.235f), new Vector3(0.34f, 0.48f, 0.05f), Vector3.Zero, "Zephyr_Cyan");
        model.AddBox("Backpack", new Vector3(0f, 1.96f, -0.28f), new Vector3(0.46f, 0.54f, 0.22f), Vector3.Zero, "Zephyr_Gunmetal");
        model.AddCylinder("Jet_Core", new Vector3(0f, 1.72f, -0.42f), new Vector3(0f, 2.13f, -0.42f), 0.12f, "Zephyr_Cyan");
        model.AddEllipsoid("Head", new Vector3(0f, 2.43f, 0f), new Vector3(0.31f, 0.34f, 0.3f), "Zephyr_White");
        model.AddBox("Cockpit_Visor", new Vector3(0f, 2.43f, 0.285f), new Vector3(0.43f, 0.14f, 0.045f), Vector3.Zero, "Zephyr_Visor");
        model.AddBox("Helmet", new Vector3(0f, 2.66f, -0.02f), new Vector3(0.15f, 0.3f, 0.18f), new Vector3(-12f, 0f, 0f), "Zephyr_Cyan");

        Vector3 leftShoulder = new(-0.5f, 2.08f, 0f);
        Vector3 rightShoulder = new(0.5f, 2.08f, 0f);
        Vector3 leftElbow = new(-0.7f, 1.63f, 0.01f);
        Vector3 rightElbow = new(0.7f, 1.63f, 0.01f);
        Vector3 leftWrist = new(-0.79f, 1.2f, 0.05f);
        Vector3 rightWrist = new(0.79f, 1.2f, 0.05f);
        model.AddCylinder("L_Upper_Arm", leftShoulder, leftElbow, 0.15f, "Zephyr_White");
        model.AddCylinder("R_Upper_Arm", rightShoulder, rightElbow, 0.15f, "Zephyr_White");
        model.AddCylinder("L_Forearm", leftElbow, leftWrist, 0.14f, "Zephyr_Gunmetal");
        model.AddCylinder("R_Forearm", rightElbow, rightWrist, 0.14f, "Zephyr_Gunmetal");
        model.AddCylinder("L_Forearm_Nozzle", new Vector3(-0.73f, 1.49f, -0.08f), new Vector3(-0.73f, 1.31f, -0.2f), 0.12f, "Zephyr_Cyan");
        model.AddCylinder("R_Forearm_Nozzle", new Vector3(0.73f, 1.49f, -0.08f), new Vector3(0.73f, 1.31f, -0.2f), 0.12f, "Zephyr_Cyan");
        model.AddEllipsoid("L_Glove", leftWrist, new Vector3(0.14f, 0.16f, 0.13f), "Zephyr_White");
        model.AddEllipsoid("R_Glove", rightWrist, new Vector3(0.14f, 0.16f, 0.13f), "Zephyr_White");

        Vector3 leftHip = new(-0.24f, 1.3f, 0f);
        Vector3 rightHip = new(0.24f, 1.3f, 0f);
        Vector3 leftKnee = new(-0.24f, 0.77f, 0f);
        Vector3 rightKnee = new(0.24f, 0.77f, 0f);
        Vector3 leftAnkle = new(-0.24f, 0.22f, 0f);
        Vector3 rightAnkle = new(0.24f, 0.22f, 0f);
        model.AddCylinder("L_Thigh", leftHip, leftKnee, 0.19f, "Zephyr_White");
        model.AddCylinder("R_Thigh", rightHip, rightKnee, 0.19f, "Zephyr_White");
        model.AddCylinder("L_Shin", leftKnee, leftAnkle, 0.16f, "Zephyr_Gunmetal");
        model.AddCylinder("R_Shin", rightKnee, rightAnkle, 0.16f, "Zephyr_Gunmetal");
        model.AddCylinder("L_Calf_Nozzle", new Vector3(-0.24f, 0.58f, -0.13f), new Vector3(-0.24f, 0.34f, -0.27f), 0.14f, "Zephyr_Cyan");
        model.AddCylinder("R_Calf_Nozzle", new Vector3(0.24f, 0.58f, -0.13f), new Vector3(0.24f, 0.34f, -0.27f), 0.14f, "Zephyr_Cyan");
        model.AddBox("L_Shoe", new Vector3(-0.24f, 0.09f, 0.17f), new Vector3(0.36f, 0.2f, 0.62f), Vector3.Zero, "Zephyr_White");
        model.AddBox("R_Shoe", new Vector3(0.24f, 0.09f, 0.17f), new Vector3(0.36f, 0.2f, 0.62f), Vector3.Zero, "Zephyr_White");

        model.AddBox("Left_Wing", new Vector3(-0.62f, 2.03f, -0.26f), new Vector3(0.92f, 0.12f, 0.32f), new Vector3(0f, -12f, 22f), "Zephyr_White");
        model.AddBox("Right_Wing", new Vector3(0.62f, 2.03f, -0.26f), new Vector3(0.92f, 0.12f, 0.32f), new Vector3(0f, 12f, -22f), "Zephyr_White");
        model.AddBox("Left_Wing_Tip", new Vector3(-1.02f, 2.19f, -0.24f), new Vector3(0.42f, 0.1f, 0.2f), new Vector3(0f, -12f, 28f), "Zephyr_Magenta");
        model.AddBox("Right_Wing_Tip", new Vector3(1.02f, 2.19f, -0.24f), new Vector3(0.42f, 0.1f, 0.2f), new Vector3(0f, 12f, -28f), "Zephyr_Magenta");
        AddRacket(model, "Zephyr_Cyan", "Zephyr_Gunmetal", 1.08f, 0.56f);

        model.Write(Path.Combine(output, baseName + ".obj"));
        WriteMaterials(Path.Combine(output, baseName + ".mtl"), new Dictionary<string, Vector3>
        {
            ["Zephyr_White"] = new(0.82f, 0.88f, 0.92f),
            ["Zephyr_Gunmetal"] = new(0.055f, 0.08f, 0.13f),
            ["Zephyr_Cyan"] = new(0.02f, 0.64f, 0.92f),
            ["Zephyr_Magenta"] = new(0.92f, 0.04f, 0.38f),
            ["Zephyr_Visor"] = new(0.015f, 0.13f, 0.24f),
            ["Paper"] = new(0.92f, 0.95f, 0.9f)
        });
    }

    private static void BuildPoko(string output)
    {
        const string baseName = "Poko_Tanuki_Model_v1";
        ObjWriter model = new ObjWriter(baseName + ".mtl");

        model.AddEllipsoid("Torso", new Vector3(0f, 1.4f, 0f), new Vector3(0.48f, 0.55f, 0.39f), "Poko_Cream");
        model.AddBox("Haragake_Lower", new Vector3(0f, 1.13f, 0.34f), new Vector3(0.52f, 0.42f, 0.055f), Vector3.Zero, "Poko_Cream");
        model.AddBox("Shorts", new Vector3(0f, 0.98f, 0f), new Vector3(0.68f, 0.28f, 0.44f), Vector3.Zero, "Poko_Ink");
        model.AddBox("Happi_Left", new Vector3(-0.28f, 1.42f, 0.2f), new Vector3(0.27f, 0.7f, 0.09f), new Vector3(0f, 0f, -7f), "Poko_Green");
        model.AddBox("Happi_Right", new Vector3(0.28f, 1.42f, 0.2f), new Vector3(0.27f, 0.7f, 0.09f), new Vector3(0f, 0f, 7f), "Poko_Green");
        model.AddEllipsoid("Head", new Vector3(0f, 1.92f, 0f), new Vector3(0.5f, 0.46f, 0.42f), "Poko_Brown");
        model.AddEllipsoid("Muzzle", new Vector3(0f, 1.82f, 0.38f), new Vector3(0.27f, 0.2f, 0.12f), "Poko_Cream");
        model.AddEllipsoid("Left_Ear", new Vector3(-0.31f, 2.22f, -0.02f), new Vector3(0.19f, 0.2f, 0.13f), "Poko_Dark");
        model.AddEllipsoid("Right_Ear", new Vector3(0.31f, 2.22f, -0.02f), new Vector3(0.19f, 0.2f, 0.13f), "Poko_Dark");
        model.AddEllipsoid("Left_Eye_Liner", new Vector3(-0.17f, 1.99f, 0.38f), new Vector3(0.16f, 0.11f, 0.035f), "Poko_Dark");
        model.AddEllipsoid("Right_Eye_Liner", new Vector3(0.17f, 1.99f, 0.38f), new Vector3(0.16f, 0.11f, 0.035f), "Poko_Dark");
        model.AddEllipsoid("Left_Eye", new Vector3(-0.17f, 2f, 0.416f), new Vector3(0.045f, 0.052f, 0.02f), "Poko_Green");
        model.AddEllipsoid("Right_Eye", new Vector3(0.17f, 2f, 0.416f), new Vector3(0.045f, 0.052f, 0.02f), "Poko_Green");
        model.AddEllipsoid("Nose", new Vector3(0f, 1.88f, 0.49f), new Vector3(0.07f, 0.055f, 0.045f), "Poko_Ink");
        model.AddEllipsoid("Head_Leaf", new Vector3(0f, 2.34f, 0.05f), new Vector3(0.21f, 0.035f, 0.34f), "Poko_Leaf");
        model.AddCylinder("Leaf_Stem", new Vector3(0f, 2.33f, -0.08f), new Vector3(0f, 2.48f, -0.14f), 0.025f, "Poko_Leaf");

        Vector3 leftShoulder = new(-0.4f, 1.56f, 0f);
        Vector3 rightShoulder = new(0.4f, 1.56f, 0f);
        Vector3 leftElbow = new(-0.58f, 1.25f, 0.03f);
        Vector3 rightElbow = new(0.58f, 1.25f, 0.03f);
        Vector3 leftWrist = new(-0.67f, 0.95f, 0.08f);
        Vector3 rightWrist = new(0.67f, 0.95f, 0.08f);
        model.AddCylinder("L_Upper_Arm", leftShoulder, leftElbow, 0.15f, "Poko_Brown");
        model.AddCylinder("R_Upper_Arm", rightShoulder, rightElbow, 0.15f, "Poko_Brown");
        model.AddCylinder("L_Forearm", leftElbow, leftWrist, 0.135f, "Poko_Brown");
        model.AddCylinder("R_Forearm", rightElbow, rightWrist, 0.135f, "Poko_Brown");
        model.AddEllipsoid("L_Glove", leftWrist, new Vector3(0.15f, 0.16f, 0.13f), "Poko_Dark");
        model.AddEllipsoid("R_Glove", rightWrist, new Vector3(0.15f, 0.16f, 0.13f), "Poko_Dark");

        Vector3 leftHip = new(-0.22f, 1f, 0f);
        Vector3 rightHip = new(0.22f, 1f, 0f);
        Vector3 leftKnee = new(-0.22f, 0.55f, 0f);
        Vector3 rightKnee = new(0.22f, 0.55f, 0f);
        Vector3 leftAnkle = new(-0.22f, 0.16f, 0f);
        Vector3 rightAnkle = new(0.22f, 0.16f, 0f);
        model.AddCylinder("L_Thigh", leftHip, leftKnee, 0.18f, "Poko_Brown");
        model.AddCylinder("R_Thigh", rightHip, rightKnee, 0.18f, "Poko_Brown");
        model.AddCylinder("L_Shin", leftKnee, leftAnkle, 0.15f, "Poko_Brown");
        model.AddCylinder("R_Shin", rightKnee, rightAnkle, 0.15f, "Poko_Brown");
        model.AddBox("L_Shoe", new Vector3(-0.22f, 0.08f, 0.13f), new Vector3(0.34f, 0.17f, 0.48f), Vector3.Zero, "Poko_Gold");
        model.AddBox("R_Shoe", new Vector3(0.22f, 0.08f, 0.13f), new Vector3(0.34f, 0.17f, 0.48f), Vector3.Zero, "Poko_Gold");

        model.AddEllipsoid("Tail_0", new Vector3(-0.32f, 1.08f, -0.34f), new Vector3(0.38f, 0.42f, 0.32f), "Poko_Brown");
        model.AddEllipsoid("Tail_1", new Vector3(-0.67f, 1.25f, -0.43f), new Vector3(0.42f, 0.48f, 0.35f), "Poko_Dark");
        model.AddEllipsoid("Tail_2", new Vector3(-0.97f, 1.48f, -0.44f), new Vector3(0.38f, 0.47f, 0.34f), "Poko_Cream");
        AddRacket(model, "Poko_Gold", "Poko_Green", 0.88f, 0.48f);

        model.Write(Path.Combine(output, baseName + ".obj"));
        WriteMaterials(Path.Combine(output, baseName + ".mtl"), new Dictionary<string, Vector3>
        {
            ["Poko_Brown"] = new(0.55f, 0.29f, 0.12f),
            ["Poko_Dark"] = new(0.12f, 0.07f, 0.045f),
            ["Poko_Cream"] = new(0.88f, 0.72f, 0.48f),
            ["Poko_Green"] = new(0.1f, 0.36f, 0.18f),
            ["Poko_Leaf"] = new(0.36f, 0.72f, 0.12f),
            ["Poko_Gold"] = new(0.76f, 0.48f, 0.12f),
            ["Poko_Ink"] = new(0.02f, 0.025f, 0.04f),
            ["Paper"] = new(0.92f, 0.95f, 0.9f)
        });
    }

    private static void AddSharedHumanBody(ObjWriter model, string torso, string accent, string skin, string hair,
        float shoulderX, float heightOffset)
    {
        float y = heightOffset;
        model.AddBox("Torso", new Vector3(0f, 1.72f + y, 0f), new Vector3(0.68f, 0.72f, 0.4f), Vector3.Zero, torso);
        model.AddBox("Chest_Accent", new Vector3(0f, 1.78f + y, 0.215f), new Vector3(0.24f, 0.58f, 0.035f), Vector3.Zero, accent);
        model.AddBox("Shorts", new Vector3(0f, 1.26f + y, 0f), new Vector3(0.76f, 0.32f, 0.44f), Vector3.Zero, torso);
        model.AddBox("Waist_Dark", new Vector3(0f, 1.43f + y, 0f), new Vector3(0.78f, 0.12f, 0.46f), Vector3.Zero, accent);
        model.AddEllipsoid("Head", new Vector3(0f, 2.28f + y, 0f), new Vector3(0.32f, 0.37f, 0.3f), skin);
        model.AddEllipsoid("Hair_Tuft_0", new Vector3(0f, 2.4f + y, -0.055f), new Vector3(0.37f, 0.28f, 0.31f), hair);
        model.AddEllipsoid("Left_Eye", new Vector3(-0.1f, 2.3f + y, 0.29f), new Vector3(0.045f, 0.065f, 0.025f), "Eye_Cyan");
        model.AddEllipsoid("Right_Eye", new Vector3(0.1f, 2.3f + y, 0.29f), new Vector3(0.045f, 0.065f, 0.025f), "Eye_Cyan");

        Vector3 leftShoulder = new(-shoulderX, 1.92f + y, 0f);
        Vector3 rightShoulder = new(shoulderX, 1.92f + y, 0f);
        Vector3 leftElbow = new(-0.65f, 1.54f + y, 0f);
        Vector3 rightElbow = new(0.65f, 1.54f + y, 0f);
        Vector3 leftWrist = new(-0.76f, 1.15f + y, 0.02f);
        Vector3 rightWrist = new(0.76f, 1.15f + y, 0.02f);
        model.AddCylinder("L_Upper_Arm", leftShoulder, leftElbow, 0.13f, skin);
        model.AddCylinder("R_Upper_Arm", rightShoulder, rightElbow, 0.13f, skin);
        model.AddCylinder("L_Forearm", leftElbow, leftWrist, 0.115f, skin);
        model.AddCylinder("R_Forearm", rightElbow, rightWrist, 0.115f, skin);
        model.AddEllipsoid("L_Glove", leftWrist, new Vector3(0.14f, 0.17f, 0.13f), accent);
        model.AddEllipsoid("R_Glove", rightWrist, new Vector3(0.14f, 0.17f, 0.13f), accent);

        Vector3 leftHip = new(-0.22f, 1.2f + y, 0f);
        Vector3 rightHip = new(0.22f, 1.2f + y, 0f);
        Vector3 leftKnee = new(-0.24f, 0.69f + y, 0f);
        Vector3 rightKnee = new(0.24f, 0.69f + y, 0f);
        Vector3 leftAnkle = new(-0.24f, 0.2f + y, 0f);
        Vector3 rightAnkle = new(0.24f, 0.2f + y, 0f);
        model.AddCylinder("L_Thigh", leftHip, leftKnee, 0.18f, skin);
        model.AddCylinder("R_Thigh", rightHip, rightKnee, 0.18f, skin);
        model.AddCylinder("L_Shin", leftKnee, leftAnkle, 0.145f, skin);
        model.AddCylinder("R_Shin", rightKnee, rightAnkle, 0.145f, skin);
        model.AddBox("L_Shoe", new Vector3(-0.24f, 0.08f + y, 0.14f), new Vector3(0.34f, 0.18f, 0.54f), Vector3.Zero, torso);
        model.AddBox("R_Shoe", new Vector3(0.24f, 0.08f + y, 0.14f), new Vector3(0.34f, 0.18f, 0.54f), Vector3.Zero, torso);
    }

    private static void AddRacket(ObjWriter model, string frame, string grip, float x, float handleBottom)
    {
        model.AddCylinder("Racket_Handle", new Vector3(x, handleBottom, 0.12f), new Vector3(x, 1.2f, 0.12f), 0.055f, grip);
        model.AddEllipticalRing("Racket_Frame", new Vector3(x, 1.57f, 0.12f), 0.31f, 0.42f, 0.035f, frame);
        model.AddBox("Racket_Strings_H", new Vector3(x, 1.57f, 0.12f), new Vector3(0.53f, 0.025f, 0.025f), Vector3.Zero, "Paper");
        model.AddBox("Racket_Strings_V", new Vector3(x, 1.57f, 0.12f), new Vector3(0.025f, 0.72f, 0.025f), Vector3.Zero, "Paper");
    }

    private static void WriteMaterials(string path, IReadOnlyDictionary<string, Vector3> colors)
    {
        StringBuilder text = new StringBuilder();
        foreach ((string name, Vector3 color) in colors)
        {
            text.AppendLine("newmtl " + name);
            text.AppendLine(FormattableString.Invariant($"Kd {color.X:0.###} {color.Y:0.###} {color.Z:0.###}"));
            text.AppendLine("Ka 0.02 0.02 0.02");
            text.AppendLine("Ks 0.08 0.08 0.08");
            text.AppendLine("Ns 18");
            text.AppendLine();
        }
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }
}

internal sealed class ObjWriter
{
    private readonly StringBuilder text = new StringBuilder();
    private int vertexOffset = 1;

    public ObjWriter(string materialFile)
    {
        text.AppendLine("# Pride Court deterministic low-poly character model");
        text.AppendLine("mtllib " + materialFile);
        text.AppendLine("s off");
    }

    public void AddBox(string name, Vector3 center, Vector3 size, Vector3 eulerDegrees, string material)
    {
        Vector3 half = size * 0.5f;
        Vector3[] local =
        {
            new(-half.X,-half.Y,-half.Z), new(half.X,-half.Y,-half.Z), new(half.X,half.Y,-half.Z), new(-half.X,half.Y,-half.Z),
            new(-half.X,-half.Y,half.Z), new(half.X,-half.Y,half.Z), new(half.X,half.Y,half.Z), new(-half.X,half.Y,half.Z)
        };
        Quaternion rotation = Quaternion.CreateFromYawPitchRoll(Deg(eulerDegrees.Y), Deg(eulerDegrees.X), Deg(eulerDegrees.Z));
        Vector3[] vertices = local.Select(v => Vector3.Transform(v, rotation) + center).ToArray();
        int[][] faces =
        {
            new[]{0,3,2,1}, new[]{4,5,6,7}, new[]{0,1,5,4}, new[]{3,7,6,2}, new[]{1,2,6,5}, new[]{0,4,7,3}
        };
        AddMesh(name, material, vertices, faces);
    }

    public void AddEllipsoid(string name, Vector3 center, Vector3 radius, string material, int segments = 10, int rings = 5)
    {
        List<Vector3> vertices = new List<Vector3>();
        for (int r = 0; r <= rings; r++)
        {
            float phi = MathF.PI * r / rings;
            for (int s = 0; s < segments; s++)
            {
                float theta = MathF.Tau * s / segments;
                vertices.Add(center + new Vector3(
                    radius.X * MathF.Sin(phi) * MathF.Cos(theta),
                    radius.Y * MathF.Cos(phi),
                    radius.Z * MathF.Sin(phi) * MathF.Sin(theta)));
            }
        }
        List<int[]> faces = new List<int[]>();
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                faces.Add(new[] { r * segments + s, (r + 1) * segments + s, (r + 1) * segments + next, r * segments + next });
            }
        AddMesh(name, material, vertices, faces);
    }

    public void AddCylinder(string name, Vector3 start, Vector3 end, float radius, string material, int segments = 8)
    {
        Vector3 axis = Vector3.Normalize(end - start);
        Vector3 reference = MathF.Abs(Vector3.Dot(axis, Vector3.UnitY)) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
        Vector3 u = Vector3.Normalize(Vector3.Cross(axis, reference));
        Vector3 v = Vector3.Normalize(Vector3.Cross(axis, u));
        List<Vector3> vertices = new List<Vector3>();
        for (int ring = 0; ring < 2; ring++)
        {
            Vector3 center = ring == 0 ? start : end;
            for (int i = 0; i < segments; i++)
            {
                float angle = MathF.Tau * i / segments;
                vertices.Add(center + u * (MathF.Cos(angle) * radius) + v * (MathF.Sin(angle) * radius));
            }
        }
        List<int[]> faces = new List<int[]> { Enumerable.Range(0, segments).Reverse().ToArray(), Enumerable.Range(segments, segments).ToArray() };
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            faces.Add(new[] { i, next, segments + next, segments + i });
        }
        AddMesh(name, material, vertices, faces);
    }

    public void AddEllipticalRing(string name, Vector3 center, float radiusX, float radiusY, float tube, string material,
        int segments = 14, int sides = 4)
    {
        List<Vector3> vertices = new List<Vector3>();
        for (int i = 0; i < segments; i++)
        {
            float theta = MathF.Tau * i / segments;
            for (int s = 0; s < sides; s++)
            {
                float phi = MathF.Tau * s / sides;
                vertices.Add(center + new Vector3(
                    (radiusX + MathF.Cos(phi) * tube) * MathF.Cos(theta),
                    (radiusY + MathF.Cos(phi) * tube) * MathF.Sin(theta),
                    MathF.Sin(phi) * tube));
            }
        }
        List<int[]> faces = new List<int[]>();
        for (int i = 0; i < segments; i++)
            for (int s = 0; s < sides; s++)
            {
                int nextI = (i + 1) % segments;
                int nextS = (s + 1) % sides;
                faces.Add(new[] { i * sides + s, nextI * sides + s, nextI * sides + nextS, i * sides + nextS });
            }
        AddMesh(name, material, vertices, faces);
    }

    private void AddMesh(string name, string material, IReadOnlyList<Vector3> vertices, IReadOnlyList<int[]> faces)
    {
        text.AppendLine();
        text.AppendLine("o " + name);
        text.AppendLine("g " + name);
        text.AppendLine("usemtl " + material);
        foreach (Vector3 vertex in vertices)
            text.AppendLine(FormattableString.Invariant($"v {vertex.X:0.######} {vertex.Y:0.######} {vertex.Z:0.######}"));
        foreach (int[] face in faces)
            text.AppendLine("f " + string.Join(' ', face.Select(index => (vertexOffset + index).ToString(CultureInfo.InvariantCulture))));
        vertexOffset += vertices.Count;
    }

    public void Write(string path)
    {
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static float Deg(float degrees) => degrees * MathF.PI / 180f;
}
