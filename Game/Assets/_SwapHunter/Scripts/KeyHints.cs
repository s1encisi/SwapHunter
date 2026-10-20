using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SwapHunter
{
    // Authored text keeps action identifiers; display names are resolved from the live binding table.
    public static class KeyHints
    {
        static readonly Regex Token = new Regex(@"\{key:(\d+)\}", RegexOptions.Compiled);
        static readonly Dictionary<string,string> cache = new Dictionary<string,string>();
        static int frame=-1, signature;
        public static int Revision { get { Refresh(); return signature; } }
        static void Refresh()
        {
            if(frame==Time.frameCount)return;frame=Time.frameCount;
            unchecked
            {
                int hash=17;var options=DemoGame.I?DemoGame.I.options:null;
                if(options?.actions!=null)foreach(var a in options.actions)
                {hash=hash*31+(a?.primary?.GetHashCode()??0);hash=hash*31+(a?.secondary?.GetHashCode()??0);}
                if(hash!=signature){signature=hash;cache.Clear();}
            }
        }
        public static void Invalidate(){frame=-1;cache.Clear();}
        public static string Key(int action)
        {
            var actions=DemoGame.I?DemoGame.I.options?.actions:null;
            if(actions==null||action<0||action>=actions.Length||actions[action]==null)return "未绑定";
            string code=!string.IsNullOrEmpty(actions[action].primary)?actions[action].primary:actions[action].secondary;
            return BindingInput.Label(code);
        }
        public static string Resolve(string text)
        {
            if(string.IsNullOrEmpty(text))return text??"";Refresh();
            if(cache.TryGetValue(text,out var result))return result;
            string value=text;
            result=Token.Replace(value,m=>Key(int.Parse(m.Groups[1].Value)));
            if(cache.Count>2048)cache.Clear();cache[text]=result;return result;
        }
    }
    public sealed class KeyHintWorldLabel:MonoBehaviour
    {
        TextMesh label;string template,lastRendered;int revision;
        public static void Attach(TextMesh label)
        {if(label&&!label.GetComponent<KeyHintWorldLabel>())label.gameObject.AddComponent<KeyHintWorldLabel>();}
        void Awake(){label=GetComponent<TextMesh>();if(label)template=label.text;}
        void LateUpdate()
        {
            if(!label)return;
            bool changed=label.text!=lastRendered;
            if(changed)template=label.text;
            int current=KeyHints.Revision;if(!changed&&revision==current)return;
            revision=current;lastRendered=KeyHints.Resolve(template);label.text=lastRendered;
            if(label.font)label.font.RequestCharactersInTexture(lastRendered,label.fontSize);
        }
    }
}
