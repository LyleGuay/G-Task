using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Collections;
using System.Collections.Generic;

public class TaskViewerWindow : EditorWindow
{
    public enum ElementType {Task, Node};

    class TaskNode
    {
        public string name;
        public bool folded;
        bool isRoot;
        public List<Task> tasks = new List<Task>();
        public List<TaskNode> childNodes = new List<TaskNode>();

        public TaskNode()
        {
            name = "New Node";
            folded = false;
            isRoot = false;
        }

        public int TotalChildren()
        {
            int children = tasks.Count;

            foreach(var child in childNodes) {
                children += child.TotalChildren();
            }

            return children;
        }

        public int TotalDoneChildren()
        {
            int children = 0;

            foreach(var task in tasks) {
                children += task.done ? 1 : 0;
            }

            foreach(var child in childNodes) {
                children += child.TotalDoneChildren();
            }

            return children;
        }

        public void Remove(TaskNode node)
        {
            if(childNodes.Contains(node)) {
                childNodes.Remove(node);
                return;
            }

            foreach(var child in childNodes) {
                child.Remove(node);
            }
        }

        public void Remove(Task taskToRemove)
        {
            if(tasks.Contains(taskToRemove)) {
                tasks.Remove(taskToRemove);
            }

            foreach(var child in childNodes) {
                child.Remove(taskToRemove);
            }
        }

        public TaskNode(XmlElement element)
        {
            isRoot = element.Name == "Tasks";

            name = element.GetSafeValue("name", "New Node");
            folded = element.GetSafeBool("folded", false);

            foreach(XmlElement child in element.ChildNodes) {
                if(child.Name == "Task") {
                    tasks.Add(new Task(child));
                } else if(child.Name == "Node") {
                    childNodes.Add(new TaskNode(child));
                } else {
                    throw new UnityException("Unknown node name " + child.Name);
                }
            }
        }

        public void ToXml(ref StringBuilder output, int depth)
        {
            string nodeName = isRoot ? "Tasks" : "Node";
            output.AppendLine(string.Format("{0}<{1} name=\"{2}\" folded=\"{3}\">", Tabs(depth), nodeName, name, folded));

            foreach(var node in childNodes) {
                node.ToXml(ref output, depth + 1);
            }

            foreach(var task in tasks) {
                task.ToXml(ref output, depth + 1);
            }

            output.AppendLine(string.Format("{0}</{1}>", Tabs(depth), nodeName));
        }

        public static string Tabs(int depth)
        {
            string tabs = "";

            for(int i = 0; i < depth; i++) {
                tabs += "\t";
            }

            return tabs;
        }
    }

    class Task
    {
        public string name;
        public bool done;

        public Task()
        {
            name = "New Task";
            done = false;
        }

        public Task(XmlElement element)
        {
            name = element.GetSafeValue("name", "New Task");
            done = element.GetSafeBool("done", false);
        }

        public void ToXml(ref StringBuilder output, int depth)
        {
            output.AppendLine(string.Format("{0}<Task name=\"{1}\" done=\"{2}\"/>", TaskNode.Tabs(depth), name, done));
        }
    }

    static class Styles
    {
        public static GUIStyle button;
        public static GUIStyle headerLabel;
        public static GUIStyle itemLabel;

        static Styles()
        {
            button = new GUIStyle(GUI.skin.button);
            button.normal.background = null;
            button.fontStyle = FontStyle.Bold;
            button.richText = true;

            headerLabel = new GUIStyle(GUI.skin.label);
            headerLabel.fontSize = 30;
            headerLabel.fontStyle = FontStyle.Bold;
            headerLabel.alignment = TextAnchor.UpperCenter;

            itemLabel = new GUIStyle(GUI.skin.label);
            itemLabel.fontSize = 12;
            itemLabel.fontStyle = FontStyle.Bold;
        }
    }

    static class Layouts
    {
        public static readonly GUILayoutOption[] BASE_LABEL = new GUILayoutOption[]
        {
            GUILayout.MaxWidth(20f)
        };

        public static readonly GUILayoutOption[] TINY_BUTTON = new GUILayoutOption[]
        {
            GUILayout.MaxWidth(20f)
        };

        public static readonly GUILayoutOption[] SMALL_BUTTON = new GUILayoutOption[]
        {
            GUILayout.MaxWidth(55f)
        };

        public static readonly GUILayoutOption[] FOLDOUT = new GUILayoutOption[]
        {
            GUILayout.MaxWidth(80f)
        };

        public static readonly GUILayoutOption[] LABEL = new GUILayoutOption[]
        {
            GUILayout.ExpandWidth(false)
        };

        public static readonly GUILayoutOption[] BUTTON = new GUILayoutOption[]
        {
            GUILayout.ExpandWidth(false)
        };

        public static readonly GUILayoutOption[] TOGGLE = new GUILayoutOption[]
        {
            GUILayout.ExpandWidth(false)
        };
    }

    Vector2 scrollPos;
    TaskNode baseRoot;
    object selected;
    object deleteElement = null;

    [MenuItem("Window/Task Viewer")]
    static void Create()
    {
        GetWindow<TaskViewerWindow>();
    }
    
    void OnGUI()
    {
        GUILayout.Label("G-Task Viewer", Styles.headerLabel);

        try {
            GUILayout.BeginHorizontal();
            {
                if(GUILayout.Button("+ Task", Layouts.SMALL_BUTTON)) {
                    baseRoot.tasks.Add(new Task());
                }
            
                if(GUILayout.Button("+ Node", Layouts.SMALL_BUTTON)) {
                    baseRoot.childNodes.Add(new TaskNode());
                }
            }
            GUILayout.EndHorizontal();

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            {
                DrawNodes(baseRoot);
            }
            GUILayout.EndScrollView();

            if(deleteElement != null) {
                if(deleteElement == selected) {
                    selected = null;
                }

                if(deleteElement is TaskNode) {
                    baseRoot.Remove(deleteElement as TaskNode);
                } else {
                    baseRoot.Remove(deleteElement as Task);
                }
            }

            if(selected != null) {
                GUILayout.Label("Selecting");

                if(selected is Task) {
                    Task task = selected as Task;
                    task.name = GUILayout.TextArea(task.name);

                    if(GUILayout.Button("X", Layouts.TINY_BUTTON)) {
                        baseRoot.Remove(task);
                        selected = null;
                    }
                } else {
                    TaskNode node = selected as TaskNode;
                    node.name = GUILayout.TextArea(node.name);

                    GUILayout.BeginHorizontal();
                    {
                        if(GUILayout.Button("+ Task", Layouts.SMALL_BUTTON)) {
                            node.tasks.Add(new Task());
                        }
                
                        if(GUILayout.Button("+ Node", Layouts.SMALL_BUTTON)) {
                            node.childNodes.Add(new TaskNode());
                        }

                        if(GUILayout.Button("X", Layouts.TINY_BUTTON)) {
                            baseRoot.Remove(node);
                            selected = null;
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
        } catch(Exception e) {
            Debug.LogException(e);
        }

        Repaint();
    }

    void Update()
    {
    }

    const float DEPTH_WIDTH = 40f;
    void DrawNodes(TaskNode root, int depth = 0)
    {
        foreach(var node in root.childNodes) {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(depth * DEPTH_WIDTH);
                string foldChar = node.folded ? "V" : ">";
                float percent = 0f;
                
                if(node.TotalChildren() > 0) {
                    percent = node.TotalDoneChildren() / (float)node.TotalChildren() * 100f;
                }

                string color = percent == 100f ? "green" : "black";

                if(GUILayout.Button(foldChar, Styles.button, Layouts.TINY_BUTTON)) {
                    node.folded = !node.folded;
                }

                if(GUILayout.Button(string.Format("<color={1}>{0}</color>", node.name, color), Styles.button, Layouts.BUTTON)) {
                    selected = node;
                }

                GUILayout.Label(string.Format("({0}%)", percent.ToString("#0.00")), Styles.itemLabel);
            }
            GUILayout.EndHorizontal();

            if(node.folded) {
                DrawNodes(node, depth + 1);
            }
        }

        foreach(var task in root.tasks) {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(depth * DEPTH_WIDTH);

                string color = task.done ? "green" : "black";
                string name = string.Format("<color={1}>{0}</color>", task.name, color);

                task.done = GUILayout.Toggle(task.done, "", Layouts.TOGGLE);

                if(GUILayout.Button(name, Styles.button, Layouts.BUTTON)) {
                    selected = task;
                }

                GUILayout.Space(10f);
            }
            GUILayout.EndHorizontal();
        }
    }

    static DirectoryInfo GetMainDirectory()
    {
        string path = Application.dataPath + "/G Task";
        DirectoryInfo directory = null;

        if(!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
        }

        directory = new DirectoryInfo(path);
        return directory;
    }

    void OnEnable()
    {
        DirectoryInfo directory = GetMainDirectory();

        if(!File.Exists(directory.FullName + "/tasks.xml")) {
            File.WriteAllText(directory.FullName + "/tasks.xml", "<GTask><Tasks></Tasks></GTask>");
        }
        
        string text = File.ReadAllText(directory.FullName + "/tasks.xml");

        text = text.Replace("&", "&amp;");

        Debug.Log("Input:\n" + text);

        StringBuilder log = new StringBuilder();
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(text);

        XmlElement gTask = doc.FirstChild as XmlElement;
        XmlElement tasksElement = gTask["Tasks"];

        int version = gTask.GetSafeInt("version", 0);

        log.AppendLine("GTask Version: " + version);
        log.AppendLine("Path:" + directory.FullName);

        baseRoot = new TaskNode(tasksElement);

        foreach(XmlElement taskNode in tasksElement.ChildNodes) {
            int id = taskNode.GetSafeInt("id", -1);
            string name = taskNode.GetSafeValue("name", "New Task");

            log.AppendLine(string.Format("Task {0} : {1}", id, name));
        }

        //Debug.Log(log);
        this.wantsMouseMove = true;
    }

    void Save()
    {
        Backup();

        DirectoryInfo directory = GetMainDirectory();

        StringBuilder output = new StringBuilder();

        output.AppendLine("<GTask version=\"1\"> <!-- This is auto generated xml code, please do not modify -->");

        baseRoot.ToXml(ref output, 1);

        output.AppendLine("</GTask>");

        File.WriteAllText(directory.FullName + "/tasks.xml", output.ToString());
    }

    void Backup()
    {
        DirectoryInfo directory = GetMainDirectory();
        string text = File.ReadAllText(directory.FullName + "/tasks.xml");

        string backupPath = directory.FullName + "/tasks_backup_" + DateTime.Now.ToString().Replace("/", "_").Replace(":", "_") + ".xml";

        File.WriteAllText(backupPath, text);
    }

    void OnDisable()
    {
        Save();
    }
}

public static class XmlElementExtensions
{
    public static XmlAttribute SafeAttr(this XmlElement element, string name)
    {
        EnsureAttr(element, name, "");
        return element.Attributes[name];
    }

    public static void SetValue(this XmlElement element, string name, string value)
    {
        element.Attributes[name].Value = value;
    }

    public static void SetSafeValue(this XmlElement element, string name, string value)
    {
        EnsureAttr(element, name, value);
        element.Attributes[name].Value = value;
    }

    public static string GetValue(this XmlElement element, string name)
    {
        return element.Attributes[name].Value;
    }

    public static string GetSafeValue(this XmlElement element, string name, string defaultVal)
    {
        EnsureAttr(element, name, defaultVal);
        return element.Attributes[name].Value;
    }

    // Ints
    public static int GetSafeInt(this XmlElement element, string name, int defaultVal)
    {
        EnsureAttr(element, name, defaultVal);
        return int.Parse(element.Attributes[name].Value);
    }

    public static void SetSafeInt(this XmlElement element, string name, int defaultVal)
    {
        EnsureAttr(element, name, defaultVal);
        element.Attributes[name].Value = defaultVal.ToString();
    }

    // Bools
    public static bool GetBool(this XmlElement element, string name)
    {
        return bool.Parse(element.Attributes[name].Value);
    }

    public static void SetBool(this XmlElement element, string name, bool defaultVal)
    {
        element.Attributes[name].Value = defaultVal.ToString();
    }

    public static bool GetSafeBool(this XmlElement element, string name, bool defaultVal)
    {
        EnsureAttr(element, name, defaultVal);
        return bool.Parse(element.Attributes[name].Value);
    }

    public static void SetSafeBool(this XmlElement element, string name, bool defaultVal)
    {
        EnsureAttr(element, name, defaultVal);
        element.Attributes[name].Value = defaultVal.ToString();
    }

    static void EnsureAttr<T>(XmlElement element, string name, T defaultVal)
    {
        if(!element.HasAttribute(name)) {
            XmlAttribute newAttr = element.OwnerDocument.CreateAttribute(name);
            element.Attributes.Append(newAttr);
            newAttr.Value = defaultVal.ToString();
        }
    }
}