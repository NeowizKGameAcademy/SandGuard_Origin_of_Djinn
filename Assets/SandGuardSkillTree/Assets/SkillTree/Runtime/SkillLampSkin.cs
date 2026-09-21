using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SandGuard.Skills.Unity
{
    /// <summary>Slices the supplied UI sheet at runtime while keeping all labels editable.</summary>
    public static class SkillLampSkin
    {
        static readonly Dictionary<string,Sprite> Sprites=new Dictionary<string,Sprite>();
        static Texture2D sheet, reference;
        static Sprite prompt;
        static Texture2D Sheet=>sheet?sheet:sheet=Resources.Load<Texture2D>("SandGuardLampSkin/SpriteSheet");
        static Texture2D Reference=>reference?reference:reference=Resources.Load<Texture2D>("SandGuardLampSkin/Reference");
        public static bool Available=>Sheet;

        static Sprite Slice(string name,Texture2D texture,int x,int y,int width,int height,int border=0,bool removeChecker=false,bool circular=false)
        {
            if(Sprites.TryGetValue(name,out var cached) && cached)return cached;
            if(!texture)return null;
            var rect=new Rect(x,texture.height-y-height,width,height);
            if(removeChecker || circular)
            {
                var source=texture.GetPixels(x,texture.height-y-height,width,height);
                var center=new Vector2((width-1)*.5f,(height-1)*.5f);float radius=Mathf.Min(width,height)*.5f-1f;
                for(int py=0;py<height;py++)for(int px=0;px<width;px++)
                {
                    int i=py*width+px;var c=source[i];
                    float spread=Mathf.Max(c.r,Mathf.Max(c.g,c.b))-Mathf.Min(c.r,Mathf.Min(c.g,c.b));
                    if(removeChecker && spread<.012f && c.r<.24f)c.a=0;
                    if(circular)c.a*=Mathf.Clamp01(radius+1f-Vector2.Distance(new Vector2(px,py),center));
                    source[i]=c;
                }
                var cleaned=new Texture2D(width,height,TextureFormat.RGBA32,false){name=name+" Texture"};
                cleaned.SetPixels(source);cleaned.Apply();texture=cleaned;rect=new Rect(0,0,width,height);
            }
            var sprite=Sprite.Create(texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(border,border,border,border));
            sprite.name=name;Sprites[name]=sprite;return sprite;
        }

        public static Sprite Panel=>Slice("LampPanel",Sheet,377,85,237,128,20,true);
        public static Sprite Button=>Slice("LampButton",Sheet,825,85,204,46,12,true);
        public static Sprite Background=>Slice("DesertBackground",Sheet,469,824,400,182);
        public static Sprite Lamp=>Slice("LampHeader",Reference,738,0,284,96);
        public static Sprite Node=>Slice("OrnateNode",Sheet,31,305,90,90,0,false,true);
        public static Sprite Prompt=>prompt?prompt:prompt=Resources.Load<Sprite>("SandGuardLampSkin/InteractionPromptRounded");

        public static void StyleButton(Button button,bool selected=false)
        {
            if(!button)return;
            button.image.sprite=Button;button.image.type=Image.Type.Sliced;
            button.image.color=selected?new Color(.4f,1,1):Color.white;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.55f,1,1);
            colors.pressedColor=new Color(.3f,.7f,.8f);colors.disabledColor=new Color(.4f,.4f,.4f,.8f);button.colors=colors;
        }

        public static Image Disc(Transform parent,float x,float y,float size,Color color)
        {
            var image=SkillWindowLayout.Pic(parent,"Inner Disc",DiscSprite(),color,x,y,size,size);return image;
        }

        static Sprite DiscSprite()
        {
            if(Sprites.TryGetValue("disc",out var cached) && cached)return cached;
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Skill Node Inner Disc"};
            var pixels=new Color[64*64];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float alpha=Mathf.Clamp01(32-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32)));
                pixels[y*64+x]=new Color(1,1,1,alpha);
            }
            texture.SetPixels(pixels);texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f));Sprites["disc"]=sprite;return sprite;
        }
    }
}
