using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Video;

class Node
{
    public int timems;
    
    public Vector3 DronePosition;
    public Quaternion DroneRotation;
    public Quaternion GimbalRotation;
    public int isVideo = 0;
    public int isPhoto = 0;
    public string FlyState = "";
    public string Message = "";

}

public class DroneTrack : EditorWindow {

    const string LATITUDE = "latitude";
    const string LONGITUDE = "longitude";
    const string ALTITUDEFEET = "altitude(feet)";
    const string TIMEMS = "time(millisecond)";
    const string COMPASSDEGREES = "compass_heading(degrees)";
    const string GIMBALHEADINGDEGREES = "gimbal_heading(degrees)";
    const string GIMBALPITCHDEGREES = "gimbal_pitch(degrees)";
    const string STARTVIDEO = "isVideo";
    const string CAPTUREPHOTO = "isPhoto";
    const string FLYSTATE = "flycState";
    const string MESSAGE = "message";

    Vector3 Scale = new Vector3(1, 1, 1);
    
    float TimeScale = 0.001f;
    Point StartPoint;
    double StartAltitude;

    string sTitle;
    bool groupEnabled = false;
    public TextAsset CSVFile;
    List<Node> Nodes = new List<Node>();

    enum MarkerTypes { FlightMarker, GroundMarker, VideoCameraMarker, StillCameraMarker, State, Message };
    
    bool CreateGroundMarkers = true;
    bool CreateCameraMarkers = true;

    //UI Params
    float VideoOffsetS = 0;
    float GroundMarkerHeightM = 1;
    string sPreviousFile = "";
    int DataSmoothing = 5;

    //Drone Parameters
    float FieldOfView = 78.8f; //Mavic
    VideoPlayer DroneVideoPlayer;

    // Use this for initialization
    [MenuItem ("DroneTrack/UI")]
    static void Init() { 
        DroneTrack window = (DroneTrack)EditorWindow.GetWindow(typeof(DroneTrack));
        window.Show();
    }

    void CreateMarker( Transform parent, MarkerTypes marker, Node n)
    {
        string prefab = "";
        if ( marker == MarkerTypes.VideoCameraMarker)
        {
            prefab = "VideoCamera";
        }
        if (marker == MarkerTypes.StillCameraMarker)
        {
            prefab = "StillCamera";
        }

        if (marker == MarkerTypes.State)
        {
            prefab = "FlyState";
        }

        GameObject gpPrefab = AssetDatabase.LoadAssetAtPath("Assets/DroneTrack/Prefabs/" + prefab + ".prefab", typeof(GameObject)) as GameObject;
        GameObject gog = (GameObject)Instantiate(gpPrefab);
        gog.transform.name = prefab;
        gog.transform.SetParent(parent);
        gog.transform.position = n.DronePosition;        
        gog.transform.localRotation = n.DroneRotation;

        if (marker == MarkerTypes.State)
        {
            GameObject state = gog.transform.Find("State").gameObject;
            TextMesh tm = state.GetComponent<TextMesh>();
            tm.text = n.FlyState;

            GameObject mess = gog.transform.Find("Message").gameObject;
            TextMesh tmm = mess.GetComponent<TextMesh>();
            tmm.text = n.Message;
        }

    }

    void CreatePathFromNodes()
    {
        GameObject dronetrack = new GameObject();
        dronetrack.transform.name = "DroneTrack";

        GameObject fpcontainer = new GameObject();
        fpcontainer.transform.position = Vector3.zero;
        fpcontainer.transform.name = "FlightPath";
        fpcontainer.transform.SetParent(dronetrack.transform);

        GameObject gpcontainer = new GameObject();
        gpcontainer.transform.position = Vector3.zero;
        gpcontainer.transform.name = "GroundPath";
        gpcontainer.transform.SetParent(dronetrack.transform);

        GameObject markercontainer = new GameObject();
        markercontainer.transform.position = Vector3.zero;
        markercontainer.transform.name = "Markers";
        markercontainer.transform.SetParent(dronetrack.transform);

        GameObject drone = new GameObject();
        drone.transform.SetParent(dronetrack.transform);
        Drone d = drone.AddComponent<Drone>();
        d.vp = DroneVideoPlayer;
        drone.transform.name = "Drone";
        GameObject dg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dg.transform.name = "DroneGeom";
        dg.transform.SetParent(drone.transform);
        dg.transform.localPosition = Vector3.zero;
        dg.transform.localRotation = Quaternion.identity;
        dg.transform.localScale = new Vector3(0.1f, 0.1f, 0.3f);

       

        Animation droneani = drone.AddComponent<Animation>();
        AnimationClip droneclip = new AnimationClip();
        droneclip.legacy = true;

        AnimationCurve curvex = new AnimationCurve();
        AnimationCurve curvey = new AnimationCurve();
        AnimationCurve curvez = new AnimationCurve();

        AnimationCurve curverx = new AnimationCurve();
        AnimationCurve curvery = new AnimationCurve();
        AnimationCurve curverz = new AnimationCurve();
        AnimationCurve curverw = new AnimationCurve();

        AnimationCurve curvegrx = new AnimationCurve();
        AnimationCurve curvegry = new AnimationCurve();
        AnimationCurve curvegrz = new AnimationCurve();
        AnimationCurve curvegrw = new AnimationCurve();
        List<AnimationEvent> events = new List<AnimationEvent>();

        float timems = 0;
        int c = 0;
        Node PreviousNode = null;
        GameObject PreviousObject = null;

        string PreviousFlyState = "";

        foreach ( Node n in Nodes)
        {
            if (PreviousNode == null)
            {
                PreviousNode = n;
            }
            c++;
            timems = n.timems * TimeScale + VideoOffsetS;

            if ( c % DataSmoothing == 1 ) { 
                curvex.AddKey(timems, (float)n.DronePosition.x);
                curvey.AddKey(timems, (float)n.DronePosition.y);
                curvez.AddKey(timems, (float)n.DronePosition.z);

                curverx.AddKey(timems, (float)n.DroneRotation.x);
                curvery.AddKey(timems, (float)n.DroneRotation.y);
                curverz.AddKey(timems, (float)n.DroneRotation.z);
                curverw.AddKey(timems, (float)n.DroneRotation.w);            

                curvegrx.AddKey(timems, (float)n.GimbalRotation.x);
                curvegry.AddKey(timems, (float)n.GimbalRotation.y);
                curvegrz.AddKey(timems, (float)n.GimbalRotation.z);
                curvegrw.AddKey(timems, (float)n.GimbalRotation.w);
            }

            if (( n.FlyState != PreviousFlyState)
                || (n.Message != ""))
            {
                PreviousFlyState = n.FlyState;
                CreateMarker(markercontainer.transform, MarkerTypes.State, n);
            }

            if ( n.isVideo == 1 )
            {
                CreateMarker(markercontainer.transform, MarkerTypes.VideoCameraMarker, n);
            }
            if (n.isPhoto == 1)
            {
                CreateMarker(markercontainer.transform, MarkerTypes.StillCameraMarker, n);
            }

            if (c % 10 == 1)
            {
                //GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                
                GameObject Prefab = AssetDatabase.LoadAssetAtPath("Assets/DroneTrack/Prefabs/FlightPointOrange.prefab", typeof(GameObject)) as GameObject;
                GameObject go = (GameObject)Instantiate(Prefab);
                go.transform.name = "PathNode";
                go.transform.SetParent(fpcontainer.transform);
                go.transform.position = n.DronePosition;
                //go.transform.localScale = new Vector3(0.5f, 0.2f, 1); 
                
                if (PreviousObject != null)
                {
                    go.transform.LookAt(PreviousObject.transform);
                }
                else
                {
                    go.transform.localRotation = n.DroneRotation;
                }
                PreviousObject = go;

                if (CreateGroundMarkers == true)
                {
                    GameObject gpPrefab = AssetDatabase.LoadAssetAtPath("Assets/DroneTrack/Prefabs/Arrow.prefab", typeof(GameObject)) as GameObject;
                    GameObject gog = (GameObject)Instantiate(gpPrefab);
                    gog.transform.name = "GroundPathNode";
                    gog.transform.SetParent(gpcontainer.transform);
                    gog.transform.position = n.DronePosition + new Vector3(0, -n.DronePosition.y + GroundMarkerHeightM, 0);
                    //gog.transform.localScale = new Vector3(5f, 2f, 10f);
                    gog.transform.localRotation = n.DroneRotation;
                }
            }

            if ( n.isVideo == 1 )
            {
                AnimationEvent evt = new AnimationEvent();
                evt.functionName = "StartVideo";                
                evt.time = timems;
               // events.Add(evt);                
            }

            PreviousNode = n;
        }

        droneclip.SetCurve("", typeof(Transform), "localPosition.x", curvex);
        droneclip.SetCurve("", typeof(Transform), "localPosition.y", curvey);
        droneclip.SetCurve("", typeof(Transform), "localPosition.z", curvez);

        droneclip.SetCurve("", typeof(Transform), "localRotation.x", curverx);
        droneclip.SetCurve("", typeof(Transform), "localRotation.y", curvery);
        droneclip.SetCurve("", typeof(Transform), "localRotation.z", curverz);
        droneclip.SetCurve("", typeof(Transform), "localRotation.w", curverw);

        AnimationUtility.SetAnimationEvents(droneclip, events.ToArray());        

        droneani.AddClip(droneclip, "FlightData");
        droneani.clip = droneclip;


        //gimbal 

        GameObject cam = new GameObject();
        cam.transform.name = "DroneCamera";
        cam.transform.SetParent(drone.transform);
        Camera dronecam = cam.AddComponent<Camera>();
        DroneVideoPlayer.targetCamera = dronecam;
        dronecam.fieldOfView = FieldOfView;
        dronecam.nearClipPlane = 5;
        dronecam.farClipPlane = 10000;

        Animation gimbalani = cam.AddComponent<Animation>();
        AnimationClip gimbalclip = new AnimationClip();
        gimbalclip.legacy = true;
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.transform.SetParent(cam.transform);
        g.transform.localPosition = new Vector3(0, -0.15f, 0.1f);
        g.transform.localRotation = Quaternion.identity;
        g.transform.localScale = new Vector3( 0.1f, 0.1f, 0.1f);
        g.transform.name = "GimbalBox";

        gimbalclip.SetCurve("", typeof(Transform), "localRotation.x", curvegrx);
        gimbalclip.SetCurve("", typeof(Transform), "localRotation.y", curvegry);
        gimbalclip.SetCurve("", typeof(Transform), "localRotation.z", curvegrz);
        gimbalclip.SetCurve("", typeof(Transform), "localRotation.w", curvegrw);

        gimbalani.AddClip(gimbalclip, "GimbalData");
        gimbalani.clip = gimbalclip;

    }

    void UpdateParameters()
    {

    }

    double FeetToMeters( double f )
    {
        return f * 0.3048;
    }

    void ParseCSV( string s )
    {
        Nodes = new List<Node>();

        string[] lines = s.Split('\n');

        //header on line 1
        string[] header = lines[0].Split(',');

        Debug.Log(lines[0]);

        foreach ( string h in header )
        {
            //Debug.Log(h);
        }
        double lat = 0;
        double lon = 0;
        double alt = 0;
        double deg = 0;
        double gimbalheading = 0;
        double gimbalpitch = 0;

        for ( int i = 0; i< lines.Length; i++)        
        {
            if (i == 0) //header
            {         
                continue;
            }

            string line = lines[i];

            if ( line.Length < 2 )
            {
                continue;
            }

            Node n = new Node();
            string[] items = line.Split(',');

            

            for ( int j=0; j< items.Length; j++)
            {
                if ( header[j] == LATITUDE )
                {                    
                    //n.latitude = float.Parse(items[i]);
                    lat = double.Parse(items[j]);
                }
                if (header[j] == LONGITUDE)
                {
                    //n.longitude = float.Parse(items[i]);
                    lon = double.Parse(items[j]);
                }
                if (header[j] == ALTITUDEFEET)
                {
                    //n.altitude = float.Parse(items[i]);
                    alt = FeetToMeters(double.Parse(items[j]));
                }
                if (header[j] == TIMEMS)
                {
                    n.timems  = int.Parse(items[j]);
                }
                if ( header[j] == COMPASSDEGREES )
                {
                    deg = double.Parse(items[j]);
                }

                if (header[j] == GIMBALHEADINGDEGREES)
                {
                    double t = double.Parse(items[j]);
                    if ( t != 0 ) { 
                        gimbalheading = double.Parse(items[j]);
                    }

                }
                if (header[j] == GIMBALPITCHDEGREES)
                {
                    double t = double.Parse(items[j]);
                    if (t != 0)
                    {
                        gimbalpitch = double.Parse(items[j]);
                    }
                }

                if (header[j] == STARTVIDEO)
                {                    
                    n.isVideo = int.Parse(items[j]);
                }

                if (header[j] == CAPTUREPHOTO)
                {
                    n.isPhoto = int.Parse(items[j]);
                }

                if (header[j] == FLYSTATE)
                {
                    n.FlyState= items[j];
                }

                if (header[j] == MESSAGE)
                {
                    n.Message = items[j];
                }
            }

            if ( i==1 )
            {
                StartPoint = WebMercator.LatLonToMeters(lat, lon);
                StartAltitude = alt;
            }

            Point p = WebMercator.LatLonToMeters(lat, lon);
            n.DronePosition.x = (float)((p.X - StartPoint.X) * Scale.x);
            n.DronePosition.z = (float)((p.Z - StartPoint.Z) * Scale.z );
            n.DronePosition.y = (float)((alt-StartAltitude) * Scale.y);
            n.DroneRotation = Quaternion.Euler(0, (float)deg, 0);
            n.GimbalRotation = Quaternion.Euler((float)-gimbalpitch, (float)(gimbalheading - deg), 0);
            //Debug.Log(n.GimbalRotation.eulerAngles);


            Nodes.Add(n);
            //Debug.Log(line);
        }

        CreatePathFromNodes();
    }

    void DeleteDroneTrackObject()
    {
        GameObject go = GameObject.Find("DroneTrack");
        GameObject.DestroyImmediate(go);
    }

    void LoadCSV(string path)
    {        
        string text = System.IO.File.ReadAllText(path);
        ParseCSV(text);     
    }

    void GetCSVFile()
    {
        string path = EditorUtility.OpenFilePanel("Overwrite with png", "Assets/DroneTrack", "csv");
        if (path.Length != 0)
        {
            sPreviousFile = path;
            LoadCSV(path);
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Base Settings", EditorStyles.boldLabel);
        sTitle  = EditorGUILayout.TextField("Title", sTitle);
        VideoOffsetS = EditorGUILayout.FloatField("Video Offset Time (ms)", VideoOffsetS);
        CreateGroundMarkers = EditorGUILayout.Toggle("Create Ground Markers", CreateGroundMarkers);
        CreateCameraMarkers = EditorGUILayout.Toggle("Create Camera Markers", CreateCameraMarkers);
        GroundMarkerHeightM = EditorGUILayout.FloatField("Ground Offset (m)", GroundMarkerHeightM);
        DataSmoothing = EditorGUILayout.IntField("Data Smoothing", DataSmoothing);

        DroneVideoPlayer = (VideoPlayer)EditorGUILayout.ObjectField("Video Player", DroneVideoPlayer, typeof(VideoPlayer),true);

        if ( GUILayout.Button("Load CSV...") )
        {
            GetCSVFile();
        }
        if (GUILayout.Button("Reload"))
        {
            if ( sPreviousFile != "" ) {
                DeleteDroneTrackObject();
                LoadCSV(sPreviousFile);
            }
        }
    }
}
