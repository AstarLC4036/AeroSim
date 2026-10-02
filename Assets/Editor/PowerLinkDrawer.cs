// Assets/Script/AircraftModules/Power/Editor/PowerLinkDrawer.cs   ← 必须在 Editor 文件夹里
using AeroSim.AircraftModules.Power;
using UnityEditor;
using UnityEngine;

namespace AeroSim.CompEditor
{
    [CustomPropertyDrawer(typeof(PowerLink))]
    public sealed class PowerLinkDrawer : PropertyDrawer
    {
        const float RowH = 26f, Gap = 3f;

        public override float GetPropertyHeight(SerializedProperty p, GUIContent label)
        {
            float h = EditorGUI.GetPropertyHeight(p, label, true);   // Fold
            return p.isExpanded ? h + RowH + Gap + RowH + Gap : h;   // Info + Button
        }

        public override void OnGUI(Rect pos, SerializedProperty p, GUIContent label)
        {
            EditorGUI.BeginProperty(pos, label, p);

            // ① 两个输入框（照 Unity 默认画，折叠时只有标题）
            var body = new Rect(pos.x, pos.y, pos.width, EditorGUI.GetPropertyHeight(p, label, true));
            EditorGUI.PropertyField(body, p, label, true);

            if (p.isExpanded)
            {
                //var comp = p.serializedObject.targetObject as Component;
                //var module = comp ? comp.GetComponentInParent<ElectricalModule>(true) : null;

                ElectricalModule module = p.FindPropertyRelative("module").objectReferenceValue as ElectricalModule;
                string portId = p.FindPropertyRelative("portId").stringValue;

                var bus = module ? module.mainBus : null;
                var port = bus != null ? bus.GetPort(portId) : null;

                // bus & port infomation
                var info = new Rect(pos.x, body.yMax + Gap, pos.width, RowH);
                EditorGUI.HelpBox(info, Info(module, bus, port, portId), Kind(module, bus, port));

                // ③ 一个按钮：绑定
                if (GUI.Button(new Rect(pos.x, info.yMax + Gap, pos.width, RowH), "Bind"))
                {
                    PowerLink link = fieldInfo.GetValue(p.serializedObject.targetObject) as PowerLink;
                    link.attachedBus = bus;
                    link.attachedPort = port;
                }
            }

            EditorGUI.EndProperty();
        }

        static MessageType Kind(ElectricalModule m, ElectricalModule.PowerBus b, ElectricalModule.PowerPort t)
        {
            if (m == null || b == null) return MessageType.Error;
            return t == null ? MessageType.Warning : MessageType.Info;
        }

        static string Info(ElectricalModule m, ElectricalModule.PowerBus b, ElectricalModule.PowerPort t,
                           string portId)
        {
            if (m == null) return "ElectricalModule not found";
            //if (string.IsNullOrEmpty(busId)) return "busId is null";
            //if (b == null) return $"Bus busId = \"{busId}\"  not found";
            if (string.IsNullOrEmpty(portId)) return $"Bus {b.busId} found, please enter portId";
            if (t == null) return $"Bus {b.busId} has no port \"{portId}\" (click \"Bind\" to create automatically)";

            string s = $"{b.busId} / {t.deviceId} · {t.demandPower:F0} W";
            if (Application.isPlaying) s += $" · {t.voltage:F1} V · {t.current:F1} A";
            if (b.isOverloaded) s += " · OVERLOADED";
            return s;
        }
    }
}