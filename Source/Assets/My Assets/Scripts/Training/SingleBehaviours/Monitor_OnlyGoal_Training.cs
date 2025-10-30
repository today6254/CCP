using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.MLAgents;
using System.IO;
using System.Linq;

public class Monitor_OnlyGoal_Training : MonoBehaviour
{
    [HideInInspector]
    public int frameCount = 0;
    private List<GoalAndSpawn> goalAreas;
    public float timeScale = 1f;
    [Header("環境")]
    [Tooltip("エージェント数 [2,n]")]
    public int numOfAgents;
    [Tooltip("すべて一度にではなく段階的にエージェントをスポーンします。有効時は GridSpawn を無効にしてください。")]
    public bool spawnGradually;
    [Tooltip("一度にスポーンするエージェント数 [1,10]")]
    public int spawnGraduallyNumber; // spwanGraduallyがtrueの場合に使用
    [Tooltip("スポーンポイントへ均等に配置する")]
    public bool spawnEqually;
    [Tooltip("グリッドパターンでスポーンする")]
    public bool gridSpawn;
    [Tooltip("シーン内のすべてのオブジェクトを Interaction として設定する")]
    public bool setAllInteraction;
    [Tooltip("エージェントが重みゾーンに入ってから重みを継承するまでの秒数 [0,n]")]
    public float timeToInheritWeights;
    [Tooltip("環境内での最大距離 (0,n]。距離正規化に使用")]
    public float maxDistance;
    [Tooltip("有効な場合、ゾーン外でも継承した重みを保持する")]
    public bool keepInheritWeights;
    [Tooltip("重みゾーンの半径 (0,n]")]
    public float inheritWeightsDistance;
    //------------------------------
    [Header("エージェント")]
    public GameObject agentPrefab;
    [Tooltip("重みに応じてエージェントを色付けする")]
    public bool coloredWeights;
    private GameObject agentParent;
    //------------------------------
    [Tooltip("1エピソードのみ実行")]
    public bool oneEpisodeOnly;
    [Tooltip("エピソードステップ無制限")]
    public bool unlimitedEpisodeSteps;
    [Tooltip("ゴール時にエージェントを破棄して再生成")]
    public bool disappearOnGoal;
    //------------------------------
    [Header("ホール/クロス環境用")]
    [Tooltip("スポーン方向の反対側のみをゴールに設定する")]
    public bool oppositeGoal;
    //------------------------------
    [Header("円形環境用")]
    [Tooltip("円形にエージェントをスポーン（半径: circularSpawnRadius）")]
    public bool circularSpawn;
    [Tooltip("円形スポーンの半径 (0,n]")]
    public float circularSpawnRadius;
    //------------------------------
    [Header("ミュージアム環境用")]
    [Tooltip("有効なら、エージェントは重みエリアに入るまでゴール行動で開始する。")]
    public bool demoScenes;
    [Tooltip("複数の振る舞いを同時に使用する")]
    public bool multiBehaviors;
    [Tooltip("ゴール行動を持つエージェントの割合 [0,100]（すべての割合は合計100）")]
    public float goalPercentage;
    [Tooltip("グループ行動を持つエージェントの割合 [0,100]（合計100）")]
    public float groupPercentage;
    [Tooltip("インタラクション行動を持つエージェントの割合 [0,100]（合計100）")]
    public float interactionPercentage;
    //------------------------------
    [Header("重み")]
    [Tooltip("有効にすると、min/max を調整して制限外の重みを使用できます。")]
    public bool extreameWeights;
    public float goalWeight;
    public float collisionWeight;
    public float interactWeight;
    public float groupWeight;
    //------------------------------
    [Header("重みの最小/最大")]
    public float goalMin = 0.1f;
    public float goalMax = 1.8f;
    public float collMin = 0.5f;
    public float collMax = 3.5f;
    public float interMin = -5.0f;
    public float interMax = 5.0f;
    public float groupMin = -3.0f;
    public float groupMax = 5.0f;
    //-------------------------------
    [Header("フェーズ")]
    public int phase;
    [Tooltip("フェーズを切り替える間隔（秒） [1,n]")]
    public float changePhaseInterval;
    [HideInInspector] public float scaling;
    //-------------------------------
    [Header("報酬しきい値")]
    [Tooltip("ゴール到達と判定する距離 [1,n]")]
    public float goalDistanceThreshold;
    [Tooltip("グルーピングと判断する距離 [1,n]")]
    public float groupDistanceThreshold;
    [Tooltip("インタラクションと判断する距離 [1,n]")]
    public float interactionDistanceThreshold;
    [Tooltip("グループ/インタラクションを有効にする最大近傍数 [2,n]")]
    public int maxNeighbours;
    //-------------------------------
    Toggle randomToggle;
    Slider goalSlider;
    Slider collSlider;
    Slider groupSlider;
    Slider interSlider;
    //-------------------------------
    List<GameObject> interactionObjects;
    Color obstalceColor;
    //-------------------------------
    private StatsRecorder statsRecorder;
    //-------------------------------
    [Header("ルート保存")]
    // 実行中のルートを保存したい場合は "saveRoutes" を有効にしてください
    [Tooltip("実行前に有効にするとルートを保存します")]
    public bool saveRoutes;
    // シーン再生中に "stopSaving" を有効にすると、その時点までのルートを .csv に保存します
    [Tooltip("実行中に有効にすると記録を停止してファイルを保存します")]
    public bool stopSaving = false;
    // 記録間隔（秒）
    [Tooltip("記録間隔（秒）")]
    public float timestepInterval;
    // 保存ディレクトリ
    [Tooltip("保存ディレクトリ")]
    public string directoryPath;
    //------------------------------
    private bool interactionSet = false;
    private bool environmentExportSet = false;

    // カラーピッカー
    private bool isColorReady = false;
    private List<GameObject> rPicker;
    private List<GameObject> gPicker;
    private List<GameObject> bPicker;
    private List<GameObject> aPicker;

    public class GoalAndSpawn
    {
        public Collider goalCollider;
        public List<Collider> spawnCollider;
    }

    private void Awake()
    {
        this.randomToggle = GameObject.Find("randomToggle").GetComponent<Toggle>();
        this.randomToggle.onValueChanged.AddListener(delegate { ToggleValueChanged(this.randomToggle); });
        Time.timeScale = this.timeScale;
    }

    // Start は最初のフレーム前に呼ばれます
    void Start()
    {
        // 有効な場合、CSVファイルを保存するディレクトリを作成
        if (this.saveRoutes)
        {
            Directory.CreateDirectory(this.directoryPath);
            Directory.CreateDirectory(this.directoryPath + "EnvironmentInfo/");
        }
        this.scaling = transform.localScale.x;
        this.statsRecorder = Academy.Instance.StatsRecorder;
        this.obstalceColor = GameObject.FindGameObjectWithTag("Obstacle").GetComponent<Renderer>().material.color;
        this.phase = 1;
        this.agentParent = GameObject.Find("Agents").gameObject;
        // シーン内のすべてのゴールおよびスポーンエリアを取得
        this.goalAreas = new List<GoalAndSpawn>();
        initializeGoalAreas();
        // "Obstacles" 配下のオブジェクトをインタラクション候補として取得
        this.interactionObjects = getInteractionObjects();
        // エージェントを生成
        StartCoroutine(instantiateAgents());
        if (this.demoScenes == true)
        {
            setWeights(this.collisionWeight, this.goalWeight, this.groupWeight, this.interactWeight, false);
        }
        else
            setWeights(0, 0, 0, 0, true);
        setSliderValues(true);
        setInteractionObjects();
        updateText();
        // changePhaseInterval 秒ごとにインタラクションと重みを更新
        InvokeRepeating("newPhase", changePhaseInterval, changePhaseInterval);
    }

    // "AgentPrefab" からエージェントを生成
    private IEnumerator instantiateAgents() // コルーチン　yieldまで同期的に実行される
    {
        WaitForSeconds wait = new WaitForSeconds(UnityEngine.Random.Range(3, 6));
        for (int i = 1; i <= this.numOfAgents; i++)
        {

            GameObject tempAgent = Instantiate(this.agentPrefab, new Vector3(0, 0, 0), Quaternion.identity); // プレハブが複製される
            tempAgent.transform.SetParent(this.agentParent.transform);
            tempAgent.GetComponent<Agent_GoalOnly_Training>().agentID = i;
            if (this.unlimitedEpisodeSteps)
                tempAgent.GetComponent<Agent_GoalOnly_Training>().MaxStep = 0;
            if (this.spawnGradually)
                if (i % this.spawnGraduallyNumber == 0)
                    yield return wait;
        }
    }

    void ToggleValueChanged(Toggle change)
    {
        if (this.randomToggle.isOn)
        {
            setWeights(0, 0, 0, 0, true);
            updateText();
            setSliderValues(false);
        }
    }

    // スライダーの値を更新
    private void setSliderValues(bool initial)
    {
        if (initial)
        {
            this.goalSlider = GameObject.Find("GoalSlider").GetComponent<Slider>();
            this.collSlider = GameObject.Find("CollisionSlider").GetComponent<Slider>();
            this.interSlider = GameObject.Find("InteractSlider").GetComponent<Slider>();
            this.groupSlider = GameObject.Find("GroupSlider").GetComponent<Slider>();

            this.goalSlider.minValue = this.goalMin;
            this.goalSlider.maxValue = this.goalMax;
            this.collSlider.minValue = this.collMin;
            this.collSlider.maxValue = this.collMax;
            this.interSlider.minValue = this.interMin;
            this.interSlider.maxValue = this.interMax;
            this.groupSlider.minValue = this.groupMin;
            this.groupSlider.maxValue = this.groupMax;
        }

        this.goalSlider.value = this.goalWeight;
        this.collSlider.value = this.collisionWeight;
        this.interSlider.value = this.interactWeight;
        this.groupSlider.value = this.groupWeight;
    }

    private void setRGBA()
    {
        if (this.isColorReady)
        {
            int red = ((int)(normalizeInRange(this.goalWeight, this.goalMin, this.goalMax) * 255f));
            int green = ((int)(normalizeInRange(this.interactWeight, this.interMin, this.interMax) * 255f));
            int blue = ((int)(normalizeInRange(this.groupWeight, this.groupMin, this.groupMax) * 255f));
            int alpha = ((int)(normalizeInRange(this.collisionWeight, this.collMin, this.collMax) * 255f));

            this.rPicker[0].GetComponent<InputField>().text = red.ToString();
            this.gPicker[0].GetComponent<InputField>().text = green.ToString();
            this.bPicker[0].GetComponent<InputField>().text = blue.ToString();
            this.aPicker[0].GetComponent<InputField>().text = alpha.ToString();

            this.rPicker[1].GetComponent<InputField>().text = (red / 255f).ToString("F2");
            this.gPicker[1].GetComponent<InputField>().text = (green / 255f).ToString("F2");
            this.bPicker[1].GetComponent<InputField>().text = (blue / 255f).ToString("F2");
            this.aPicker[1].GetComponent<InputField>().text = (alpha / 255f).ToString("F2");
        }
        else
        {
            this.rPicker = new List<GameObject>(2);
            this.gPicker = new List<GameObject>(2);
            this.bPicker = new List<GameObject>(2);
            this.aPicker = new List<GameObject>(2);
            this.rPicker.Add(GameObject.Find("InputFieldRed"));
            this.gPicker.Add(GameObject.Find("InputFieldGreen"));
            this.bPicker.Add(GameObject.Find("InputFieldBlue"));
            this.aPicker.Add(GameObject.Find("InputFieldAlpha"));
            this.rPicker.Add(GameObject.Find("InputFieldValueRed"));
            this.gPicker.Add(GameObject.Find("InputFieldValueGreen"));
            this.bPicker.Add(GameObject.Find("InputFieldValueBlue"));
            this.aPicker.Add(GameObject.Find("InputFieldValueAlpha"));
            ColorPicker.Create(Color.white, "", null, null, true);
            this.isColorReady = true;
        }
    }

    private void setGraphPoint()
    {
        Image bg = GameObject.Find("SquareGraphArea").GetComponent<Image>();
        RectTransform canvas = GameObject.Find("SquareGraphArea").GetComponent<RectTransform>();
        RectTransform pointer = GameObject.Find("GraphPointer").GetComponent<RectTransform>();

        float nGoal = normalizeInRange(this.goalWeight, this.goalMin, this.goalMax);
        float nInteract = normalizeInRange(this.interactWeight, this.interMin, this.interMax);
        float nGroup = normalizeInRange(this.groupWeight, this.groupMin, this.groupMax);
        float nColl = normalizeInRange(this.collisionWeight, this.collMin, this.collMax);

        GameObject.Find("goalGraphText").GetComponent<Text>().fontSize = (int)Mathf.Clamp(20f * nGoal, 4, 20);
        GameObject.Find("interactGraphText").GetComponent<Text>().fontSize = (int)Mathf.Clamp(20f * nInteract, 4, 20);
        GameObject.Find("groupGraphText").GetComponent<Text>().fontSize = (int)Mathf.Clamp(20f * nGroup, 4, 20);
        GameObject.Find("collisionGraphText").GetComponent<Text>().fontSize = (int)Mathf.Clamp(20f * nColl, 4, 20);

        Vector2 center = new Vector3(0, 0);

        Vector2 goal = new Vector2(-1f, 1f) * nGoal;
        Vector2 collision = new Vector2(1f, 1f) * nColl;
        Vector2 interact = new Vector2(1f, -1f) * nInteract;
        Vector2 group = new Vector2(-1f, -1f) * nGroup;
        center = (goal + collision + interact + group) / 4;

        float pointerPosX = center.x * (canvas.rect.width - 6f);
        float pointerPosY = center.y * (canvas.rect.height - 6f);

        pointer.anchoredPosition = new Vector2(pointerPosX, pointerPosY);

        Color color = new Color(nGoal, nInteract, nGroup, nColl);
        bg.color = color;

    }

    private void Update()
    {
        if (!this.randomToggle.isOn)
        {
            setWeights(this.collSlider.value, this.goalSlider.value, this.groupSlider.value, this.interSlider.value, false);
            updateText();
        }
        setRGBA();
        setGraphPoint();
        if (this.saveRoutes == true && this.interactionSet == true && this.environmentExportSet == false)
            saveEnvironmentInfo();
    }

    // 新しいフェーズ：ランダム有効なら新しい重みを設定
    private void newPhase()
    {
        this.phase++;
        if (this.randomToggle.isOn)
            setWeights(0, 0, 0, 0, true);
        setInteractionObjects();
        updateText();
        setSliderValues(false);
    }

    // 重みを更新（random が true ならランダム、false なら引数を使用）
    private void setWeights(float collision, float goal, float groub, float interact, bool random)
    {
        if (random)
        {
            this.collisionWeight = Random.Range((this.collMin + this.collMax) / 2.5f, this.collMax / 1.25f);

            this.goalWeight = Random.Range(0.0F, 1.0F) < 0.5F ? this.goalMin * Random.Range(0.5F, 1.0F) : this.goalMax * Random.Range(0.5F, 1.0F);
            this.groupWeight = Random.Range(0.0F, 1.0F) < 0.5F ? this.groupMin * Random.Range(0.5F, 1.0F) : this.groupMax * Random.Range(0.5F, 1.0F);
            this.interactWeight = Random.Range(0.0F, 1.0F) < 0.5F ? this.interMin * Random.Range(0.5F, 1.0F) : this.interMax * Random.Range(0.5F, 1.0F);
            //this.goalWeight = Random.Range(this.goalMin, this.goalMax);
            //this.groupWeight = Random.Range(this.groupMin, this.groupMax);
            //this.interactWeight = Random.Range(this.interMin, this.interMax);
        }
        else
        {
            this.collisionWeight = collision;
            this.goalWeight = goal;
            this.groupWeight = groub;
            this.interactWeight = interact;
        }
        statsRecorder.Add("Goal Weight", this.goalWeight);
        statsRecorder.Add("Collide Weight", this.collisionWeight);
        statsRecorder.Add("Group Weight", this.groupWeight);
        statsRecorder.Add("Interact Weight", this.interactWeight);
    }

    // 重みの表示テキストを更新
    private void updateText()
    {
        GameObject.Find("phaseText").GetComponent<Text>().text = this.phase.ToString();
        GameObject.Find("GoalW").GetComponent<Text>().text = normalizeInRange(this.goalWeight, this.goalMin, this.goalMax).ToString("0.0");
        GameObject.Find("CollisionW").GetComponent<Text>().text = normalizeInRange(this.collisionWeight, this.collMin, this.collMax).ToString("0.0");
        GameObject.Find("InteractW").GetComponent<Text>().text = normalizeInRange(this.interactWeight, this.interMin, this.interMax).ToString("0.0");
        GameObject.Find("GroupW").GetComponent<Text>().text = normalizeInRange(this.groupWeight, this.groupMin, this.groupMax).ToString("0.0");
    }

    // ゴールとスポーン領域のリストを返す
    public List<GoalAndSpawn> getGoalAreas()
    {
        return this.goalAreas;
    }

    public float normalizeInRange(float value, float min, float max)
    {
        float scaledValue = (value - min) / (max - min);
        return scaledValue;
    }

    // インタラクション対象オブジェクトのリストを返す
    private List<GameObject> getInteractionObjects()
    {
        List<GameObject> obj = new List<GameObject>();
        Transform parent = GameObject.Find("Obstacles").transform;
        if (parent.GetChild(0).gameObject.name.Contains("Parent"))
        {
            foreach (Transform child in parent)
            {
                foreach (Transform child2 in child)
                    obj.Add(child2.gameObject);
            }
        }
        else
        {
            foreach (Transform child in parent)
            {
                obj.Add(child.gameObject);
            }
        }
        return obj;
    }

    // 各フェーズで、一部のオブジェクトを Interaction に設定
    private void setInteractionObjects()
    {
        if (this.setAllInteraction)
        {
            foreach (GameObject child in this.interactionObjects)
            {
                child.tag = "Interaction"; // setAllInteractionが有効なとき
                child.GetComponent<Renderer>().material.color = Color.red;
            }
            this.interactionSet = true;
            return;
        }
        foreach (GameObject child in this.interactionObjects)
        {
            child.tag = "Obstacle"; // setAllInteractionが有効でないとき
            child.GetComponent<Renderer>().material.color = this.obstalceColor;
        }
        int len = this.interactionObjects.Count;
        int times = Random.Range(3, len);
        for (int i = 0; i < times; i++) // 3回以上ループ
        {
            int index = Random.Range(0, len); // なんのため？
            this.interactionObjects[i].tag = "Interaction";
            this.interactionObjects[i].GetComponent<Renderer>().material.color = Color.red;
        }
        this.interactionSet = true;
    }

    // シーン内のゴールとスポーンエリアを検索して初期化
    private void initializeGoalAreas()
    {
        // ゴールエリアを初期化
        GameObject[] goalAreasParent = GameObject.FindGameObjectsWithTag("GoalArea");
        foreach (GameObject area in goalAreasParent)
        {
            GoalAndSpawn temp = new GoalAndSpawn();
            temp.goalCollider = area.GetComponent<Collider>();
            temp.spawnCollider = new List<Collider>();
            for (int j = 0; j < area.transform.childCount; j++)
                temp.spawnCollider.Add(area.transform.GetChild(j).GetComponent<Collider>());
            this.goalAreas.Add(temp);
        }
    }

    private void FixedUpdate()
    {
        this.frameCount++;
        // デバッグ用：フレーム数出力（必要なら有効化）
        // print(this.frameCount);
    }

    // 全体で１回だけ呼ばれる 環境情報をCSVに書き込む
    private void saveEnvironmentInfo()
    {
        this.environmentExportSet = true;
        string filePath = this.directoryPath + "EnvironmentInfo/enviInfo.csv";
        StreamWriter writer = new StreamWriter(filePath);

        writer.WriteLine(this.maxDistance);
        writer.WriteLine(this.groupDistanceThreshold);
        writer.WriteLine(this.interactionDistanceThreshold);
        writer.WriteLine(this.maxNeighbours);

        GameObject[] interObjects = GameObject.FindGameObjectsWithTag("Interaction");
        foreach (GameObject g in interObjects)
        {
            Collider c = g.GetComponent<Collider>();
            Vector3 center = c.bounds.center;
            float minX = c.bounds.min.x;
            float maxX = c.bounds.max.x;
            float minZ = c.bounds.min.z;
            float maxZ = c.bounds.max.z;
            writer.WriteLine(center.x.ToString("F4") + ";" + center.z.ToString("F4") + ";" + minX.ToString("F4") + ";" + maxX.ToString("F4") + ";" + minZ.ToString("F4") + ";" + maxZ.ToString("F4"));
        }

        writer.Flush();
        writer.Close();
    }

    // 各エージェントのルートを CSV に保存（有効時）
    public void saveRoute(Vector3 start, Vector3 end, int agentCollisions, string name, float reward, List<float[]> list)
    {
        float threshold = -100;
        if (reward > threshold && list.Count > 5)
        {
            string filePath = this.directoryPath + name + ".csv";
            StreamWriter writer = new StreamWriter(filePath);

            int step = (int)(this.timestepInterval / Time.fixedDeltaTime);

            writer.WriteLine(start.x.ToString("F4") + ";" + start.z.ToString("F4") + ";" + end.z.ToString("F4") + ";" + end.z.ToString("F4") + ";" + agentCollisions);

            for (int i = 1; i < list.Count; i += step)
            {
                writer.WriteLine(list[i][0].ToString("F4") + ";" + list[i][1].ToString("F4") + ";" + list[i][2].ToString("F4") + ";" + list[i][3].ToString("F4") + ";" +
                    list[i][4].ToString("F4") + ";" + list[i][5].ToString("F4") + ";" + list[i][6].ToString("F4") + ";" + list[i][7].ToString("F4") + ";" + list[i][8].ToString("F4"));
            }
            writer.Flush();
            writer.Close();
        }
    }
}