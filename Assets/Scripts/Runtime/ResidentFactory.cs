using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public static class ResidentFactory
    {
        public static readonly Color[] Palette = {
            new Color(.82f,.55f,.27f), new Color(.57f,.72f,1), new Color(1,.74f,.24f), new Color(.98f,.43f,.61f),
            new Color(.43f,.8f,.66f), new Color(1,.89f,.67f), new Color(.62f,.4f,.87f)
        };
        public static Transform Create(ToyFactory art, CharacterId id, Transform parent, SkinDefinition appearance = null)
        {
            var actor = art.Root(id.ToString(),parent,Vector3.zero);
            Color coat = Palette[(int)id], skin = new Color(1,.81f,.69f), dark = new Color(.16f,.1f,.17f);
            Color accent = YardBuilder.Warm;
            bool variant = appearance != null && appearance.CharacterId == id && !appearance.Default;
            if (variant)
            {
                ColorUtility.TryParseHtmlString("#" + appearance.CoatHex,out coat);
                ColorUtility.TryParseHtmlString("#" + appearance.AccentHex,out accent);
                actor.name = id + " / " + appearance.SkinId;
            }
            Color hair = id == CharacterId.Milo || id == CharacterId.Kiko ? new Color(.29f,.16f,.13f) :
                id == CharacterId.Mochi ? new Color(1,.94f,.8f) : Color.Lerp(coat,Color.white,.18f);
            if (!variant && id == CharacterId.Nori) coat = new Color(.48f,.57f,.35f);
            art.Ball("Jacket",actor,new Vector3(0,.57f,0),new Vector3(.65f,.73f,.48f),coat);
            art.Ball("Head",actor,new Vector3(0,1.23f,0),new Vector3(.9f,.82f,.78f),skin);
            art.Ball("Hair",actor,new Vector3(0,1.48f,.07f),new Vector3(.97f,.53f,.83f),hair);
            for (int i = -3; i <= 3; i++)
            {
                var fringe = art.Ball("Layered fringe",actor,new Vector3(i * .13f,1.49f - (i % 2 == 0 ? .05f : 0),-.3f),new Vector3(.27f,.33f,.24f),Color.Lerp(hair,Color.white,(i + 3) % 3 * .035f));
                fringe.localRotation = Quaternion.Euler(0,0,-18 + i * 10);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Color iris = id == CharacterId.Lumi || id == CharacterId.Yume ? new Color(.4f,.27f,.73f) : new Color(.46f,.25f,.09f);
                art.Ball("Eye lash rim",actor,new Vector3(side * .19f,1.25f,-.361f),new Vector3(.225f,.267f,.07f),dark);
                art.Ball("Eye white",actor,new Vector3(side * .19f,1.246f,-.391f),new Vector3(.205f,.234f,.055f),new Color(1,.97f,.95f));
                art.Ball("Iris",actor,new Vector3(side * .19f,1.25f,-.42f),new Vector3(.153f,.197f,.047f),iris);
                art.Ball("Pupil",actor,new Vector3(side * .19f,1.265f,-.442f),new Vector3(.097f,.145f,.025f),dark);
                art.Ball("Eye glint",actor,new Vector3(side * .19f - .033f,1.31f,-.46f),Vector3.one * .048f,Color.white,true);
                art.Ball("Eye glint small",actor,new Vector3(side * .19f + .034f,1.205f,-.456f),Vector3.one * .024f,Color.white,true);
                art.Ball("Cheek",actor,new Vector3(side * .31f,1.11f,-.316f),new Vector3(.155f,.078f,.024f),new Color(1,.55f,.57f));
                art.Ball("Boot",actor,new Vector3(side * .2f,.16f,-.06f),new Vector3(.27f,.27f,.38f),dark);
                art.Ball("Hand",actor,new Vector3(side * .39f,.61f,-.02f),Vector3.one * .22f,skin);
            }
            art.Ball("Smile",actor,new Vector3(0,1.08f,-.385f),new Vector3(.11f,.045f,.03f),dark);
            art.Ball("Little nose",actor,new Vector3(0,1.17f,-.409f),new Vector3(.076f,.065f,.063f),new Color(1,.72f,.62f));
            art.Box("Jacket zip",actor,new Vector3(0,.65f,-.246f),new Vector3(.035f,.3f,.025f),accent);
            if (variant)
            {
                art.Ball("Scarf collar",actor,new Vector3(0,.91f,0),new Vector3(.7f,.18f,.58f),accent);
                art.Box("Scarf tail",actor,new Vector3(.21f,.7f,-.26f),new Vector3(.15f,.35f,.07f),accent);
            }
            art.Ball("Backpack",actor,new Vector3(0,.66f,.29f),new Vector3(.46f,.49f,.27f),YardBuilder.Navy);
            if (id == CharacterId.Milo || id == CharacterId.Mochi)
            {
                art.Ball("Soft hood",actor,new Vector3(0,1.64f,.05f),new Vector3(1.08f,.47f,.9f),coat);
                for (int side = -1; side <= 1; side += 2)
                {
                    art.Ball("Round ear",actor,new Vector3(side * .39f,1.85f,0),Vector3.one * .29f,coat);
                    art.Ball("Ear inset",actor,new Vector3(side * .39f,1.85f,-.115f),new Vector3(.15f,.15f,.05f),skin);
                }
            }
            else if (id == CharacterId.Kiko)
            {
                art.Ball("Builder cap",actor,new Vector3(0,1.66f,.03f),new Vector3(1,.46f,.86f),variant ? coat : YardBuilder.Navy);
                art.Ball("Cap bill",actor,new Vector3(.08f,1.58f,-.49f),new Vector3(.97f,.1f,.55f),coat);
                art.Ball("Cap button",actor,new Vector3(0,1.89f,.05f),Vector3.one * .16f,coat);
            }
            else if (id == CharacterId.Pip)
            {
                art.Box("Goggle strap",actor,new Vector3(0,1.66f,-.31f),new Vector3(.86f,.1f,.11f),YardBuilder.Navy);
                for (int side = -1; side <= 1; side += 2)
                {
                    art.Ball("Goggle rim",actor,new Vector3(side * .24f,1.73f,-.35f),new Vector3(.35f,.28f,.2f),YardBuilder.Warm);
                    art.Ball("Goggle glass",actor,new Vector3(side * .24f,1.73f,-.45f),new Vector3(.24f,.2f,.07f),new Color(.56f,.38f,.9f));
                }
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    art.Ball("Side curls",actor,new Vector3(side * .46f,1.33f,.06f),new Vector3(.42f,.61f,.5f),hair);
                    art.Ball("Hair bun",actor,new Vector3(side * .37f,1.78f,.04f),Vector3.one * .4f,hair);
                }
                for (int petal = 0; petal < 5; petal++)
                {
                    float angle = petal * Mathf.PI * .4f;
                    art.Ball("Flower petal",actor,new Vector3(-.38f + Mathf.Cos(angle) * .09f,1.57f + Mathf.Sin(angle) * .09f,-.43f),Vector3.one * .11f,accent);
                }
            }
            ResidentDetails.Add(art,actor,id,coat,accent,hair,skin);
            return actor;
        }
    }
}
