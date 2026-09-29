using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public static class ResidentDetails
    {
        public static void Add(ToyFactory art,Transform root,CharacterId id,Color coat,Color accent,Color hair,Color skin)
        {
            Color cream = new Color(1,.92f,.77f), leather = new Color(.27f,.17f,.14f);
            art.Ball("Scarf",root,new Vector3(0,.93f,-.015f),new Vector3(.73f,.17f,.58f),cream);
            art.Box("Scarf fold",root,new Vector3(-.14f,.76f,-.255f),new Vector3(.18f,.24f,.045f),cream);
            art.Ball("Shorts",root,new Vector3(0,.34f,.01f),new Vector3(.57f,.27f,.43f),leather);
            art.Box("Work belt",root,new Vector3(0,.44f,-.22f),new Vector3(.52f,.09f,.04f),leather);
            art.Box("Belt buckle",root,new Vector3(0,.44f,-.245f),new Vector3(.115f,.09f,.035f),accent);
            for (int side = -1; side <= 1; side += 2)
            {
                var sleeve = art.Ball("Padded sleeve",root,new Vector3(side * .34f,.7f,0),new Vector3(.26f,.34f,.3f),coat);
                sleeve.localRotation = Quaternion.Euler(0,0,side * 22);
                art.Ball("Soft cuff",root,new Vector3(side * .39f,.59f,-.02f),new Vector3(.245f,.12f,.25f),cream);
                art.Ball("Boot cuff",root,new Vector3(side * .2f,.255f,0),new Vector3(.28f,.12f,.26f),cream);
                art.Ball("Boot sole",root,new Vector3(side * .2f,.065f,-.067f),new Vector3(.3f,.105f,.4f),leather);
                for (int lace = 0; lace < 3; lace++)
                    art.Box("Boot lace",root,new Vector3(side * .2f,.14f + lace * .04f,-.23f),new Vector3(.13f,.017f,.018f),accent);
                art.Box("Jacket pocket",root,new Vector3(side * .17f,.56f,-.23f),new Vector3(.16f,.13f,.044f),Color.Lerp(coat,cream,.16f));
                art.Ball("Pocket button",root,new Vector3(side * .17f,.59f,-.257f),Vector3.one * .037f,accent);
                var strap = art.Box("Backpack strap",root,new Vector3(side * .23f,.73f,-.177f),new Vector3(.055f,.31f,.048f),leather);
                strap.localRotation = Quaternion.Euler(0,0,side * 12);
            }
            for (int button = 0; button < 3; button++) art.Ball("Gold button",root,new Vector3(.04f,.57f + button * .105f,-.263f),Vector3.one * .038f,accent);
            var bunny = art.Root("Bunny charm",root,new Vector3(.37f,.73f,.27f));
            art.Ball("Charm body",bunny,Vector3.zero,new Vector3(.2f,.24f,.16f),cream);
            art.Ball("Charm face",bunny,new Vector3(0,.15f,-.025f),new Vector3(.24f,.21f,.17f),cream);
            for (int side = -1; side <= 1; side += 2)
            {
                art.Ball("Charm ear",bunny,new Vector3(side * .065f,.29f,0),new Vector3(.065f,.2f,.07f),cream);
                art.Ball("Charm eye",bunny,new Vector3(side * .053f,.17f,-.105f),Vector3.one * .027f,leather);
            }
            art.Ball("Charm nose",bunny,new Vector3(0,.12f,-.113f),Vector3.one * .027f,new Color(1,.55f,.6f));
            if (id == CharacterId.Milo || id == CharacterId.Mochi)
            {
                for (int i = 0; i < 13; i++)
                {
                    float angle = (i / 12f * 210 - 15) * Mathf.Deg2Rad;
                    art.Ball("Fleece hood trim",root,new Vector3(Mathf.Cos(angle) * .5f,1.37f + Mathf.Sin(angle) * .45f,-.21f),Vector3.one * .12f,cream);
                }
            }
            else if (id != CharacterId.Kiko && id != CharacterId.Pip)
            {
                for (int side = -1; side <= 1; side += 2)
                    for (int curl = 0; curl < 6; curl++)
                    {
                        float angle = curl * Mathf.PI / 3;
                        art.Ball("Sculpted curl",root,new Vector3(side * .42f + Mathf.Cos(angle) * .1f,1.65f + Mathf.Sin(angle) * .14f,-.06f),Vector3.one * .24f,Color.Lerp(hair,cream,curl % 2 * .08f));
                    }
            }
            if (id == CharacterId.Milo)
            {
                art.Box("Mallet handle",root,new Vector3(-.49f,.45f,-.12f),new Vector3(.065f,.45f,.07f),leather);
                art.Box("Mallet head",root,new Vector3(-.49f,.22f,-.12f),new Vector3(.31f,.2f,.18f),new Color(.68f,.45f,.24f));
            }
            else if (id == CharacterId.Kiko || id == CharacterId.Lumi)
            {
                var tool = art.Root("Toy blaster",root,new Vector3(-.43f,.55f,-.12f));
                art.Ball("Blaster body",tool,Vector3.zero,new Vector3(.23f,.26f,.36f),coat);
                var barrel = art.Shape("Blaster barrel",PrimitiveType.Cylinder,tool,new Vector3(0,0,-.25f),new Vector3(.18f,.15f,.18f),accent);
                barrel.localRotation = Quaternion.Euler(90,0,0);
                art.Ball("Barrel bore",tool,new Vector3(0,0,-.398f),new Vector3(.12f,.12f,.01f),leather);
            }
            else if (id == CharacterId.Pip || id == CharacterId.Mochi)
            {
                art.Box("Wrench handle",root,new Vector3(-.44f,.38f,-.09f),new Vector3(.07f,.35f,.06f),new Color(.55f,.6f,.68f));
                art.Ball("Wrench head",root,new Vector3(-.44f,.21f,-.09f),new Vector3(.18f,.19f,.07f),new Color(.65f,.7f,.78f));
            }
            else if (id == CharacterId.Nori)
            {
                art.Box("Tool planter",root,new Vector3(0,.53f,-.38f),new Vector3(.55f,.27f,.28f),new Color(.63f,.38f,.2f));
                art.Box("Planter handle",root,new Vector3(0,.73f,-.34f),new Vector3(.05f,.24f,.045f),leather);
                art.Ball("Flower leaves",root,new Vector3(.16f,.74f,-.37f),new Vector3(.17f,.18f,.12f),new Color(.4f,.66f,.28f));
                art.Ball("Flower",root,new Vector3(.16f,.84f,-.37f),Vector3.one * .13f,cream);
            }
            else
            {
                art.Box("Lantern frame",root,new Vector3(-.5f,.39f,-.12f),new Vector3(.23f,.31f,.2f),accent);
                art.Box("Lantern light",root,new Vector3(-.5f,.39f,-.228f),new Vector3(.155f,.225f,.025f),new Color(1,.71f,.25f),true);
            }
        }
    }
}
