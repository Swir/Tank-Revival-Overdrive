using UnityEngine;

namespace TankRevival
{
    public sealed class BossLegend3DPresentation : MonoBehaviour
    {
        private BossLegendDirector _legend;
        private Transform _root;
        private Transform _core;
        private Transform _halo;
        private int _tier;
        private bool _ready;

        public void Initialize(BossLegendDirector legend, int tier)
        {
            if (_ready) return;
            _ready = true;
            _legend = legend;
            _tier = Mathf.Clamp(tier, 1, 10);
            Build();
        }

        private void Build()
        {
            Color c = _legend != null ? _legend.LegendColor() : new Color(1f,0.2f,0.05f);
            Color dark = Color.Lerp(c, Color.black, 0.62f);
            var root = new GameObject("BossLegend3D_v3_3");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, -0.72f);
            _root = root.transform;

            Runtime3DFactory.Box("CommandDeck", _root, new Vector3(0f, -0.02f, 0f), new Vector3(1.15f, 0.82f, 0.16f), dark, 0.78f, 0.34f);
            Runtime3DFactory.Cylinder("LegendCore", _root, new Vector3(0f, -0.05f, -0.15f), 0.40f, 0.12f, c, 0.46f, 0.75f);
            _core = _root.Find("LegendCore");

            switch (_tier)
            {
                case 1: BuildJackal(c,dark); break;
                case 2: BuildViper(c,dark); break;
                case 3: BuildWarden(c,dark); break;
                case 4: BuildRam(c,dark); break;
                case 5: BuildBastion(c,dark); break;
                case 6: BuildHydra(c,dark); break;
                case 7: BuildReaper(c,dark); break;
                case 8: BuildTyrant(c,dark); break;
                case 9: BuildCrown(c,dark); break;
                default: BuildZero(c,dark); break;
            }

            var halo = Runtime3DFactory.Cylinder("LegendHalo", _root, new Vector3(0f,0f,0.08f), 1.42f, 0.025f, Color.Lerp(c,Color.white,0.30f), 0.08f, 0.88f);
            _halo = halo.transform;
        }

        private void BuildJackal(Color c, Color dark)
        {
            Runtime3DFactory.Box("FangL", _root, new Vector3(-0.42f,0.40f,-0.08f), new Vector3(0.13f,0.52f,0.12f), c,0.60f,0.50f).transform.localRotation = Quaternion.Euler(0f,0f,-17f);
            Runtime3DFactory.Box("FangR", _root, new Vector3(0.42f,0.40f,-0.08f), new Vector3(0.13f,0.52f,0.12f), c,0.60f,0.50f).transform.localRotation = Quaternion.Euler(0f,0f,17f);
        }

        private void BuildViper(Color c, Color dark)
        {
            Runtime3DFactory.Box("TwinRailL", _root, new Vector3(-0.31f,0.48f,-0.09f), new Vector3(0.11f,0.70f,0.11f), c,0.72f,0.62f);
            Runtime3DFactory.Box("TwinRailR", _root, new Vector3(0.31f,0.48f,-0.09f), new Vector3(0.11f,0.70f,0.11f), c,0.72f,0.62f);
        }

        private void BuildWarden(Color c, Color dark)
        {
            for(int i=-1;i<=1;i++) Runtime3DFactory.Box("AshStack"+i,_root,new Vector3(i*0.28f,-0.26f,-0.13f),new Vector3(0.12f,0.38f,0.18f),i==0?c:dark,0.48f,0.38f);
        }

        private void BuildRam(Color c, Color dark)
        {
            Runtime3DFactory.Box("RamBlade",_root,new Vector3(0f,0.53f,-0.05f),new Vector3(1.05f,0.18f,0.15f),c,0.80f,0.42f);
            Runtime3DFactory.Box("RamSpine",_root,new Vector3(0f,0.30f,-0.10f),new Vector3(0.18f,0.56f,0.18f),dark,0.75f,0.30f);
        }

        private void BuildBastion(Color c, Color dark)
        {
            Runtime3DFactory.Box("ArmorL",_root,new Vector3(-0.55f,0f,-0.04f),new Vector3(0.22f,0.88f,0.22f),dark,0.85f,0.28f);
            Runtime3DFactory.Box("ArmorR",_root,new Vector3(0.55f,0f,-0.04f),new Vector3(0.22f,0.88f,0.22f),dark,0.85f,0.28f);
            Runtime3DFactory.Box("SiegeCrown",_root,new Vector3(0f,0.32f,-0.16f),new Vector3(0.56f,0.22f,0.16f),c,0.78f,0.52f);
        }

        private void BuildHydra(Color c, Color dark)
        {
            for(int i=-2;i<=2;i++)
            {
                var arm=Runtime3DFactory.Box("HydraBarrel"+i,_root,new Vector3(i*0.20f,0.42f,-0.10f),new Vector3(0.08f,0.62f,0.09f),c,0.66f,0.58f);
                arm.transform.localRotation=Quaternion.Euler(0f,0f,i*5f);
            }
        }

        private void BuildReaper(Color c, Color dark)
        {
            var left=Runtime3DFactory.Box("ScytheL",_root,new Vector3(-0.48f,0.18f,-0.06f),new Vector3(0.12f,0.72f,0.12f),c,0.72f,0.65f); left.transform.localRotation=Quaternion.Euler(0,0,-38f);
            var right=Runtime3DFactory.Box("ScytheR",_root,new Vector3(0.48f,0.18f,-0.06f),new Vector3(0.12f,0.72f,0.12f),c,0.72f,0.65f); right.transform.localRotation=Quaternion.Euler(0,0,38f);
        }

        private void BuildTyrant(Color c, Color dark)
        {
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*0.5f;
                Runtime3DFactory.Cylinder("VoidNode"+i,_root,new Vector3(Mathf.Cos(a)*0.52f,Mathf.Sin(a)*0.42f,-0.10f),0.20f,0.09f,c,0.35f,0.90f);
            }
        }

        private void BuildCrown(Color c, Color dark)
        {
            for(int i=0;i<5;i++)
            {
                float x=(i-2)*0.21f;
                float h=0.30f+0.08f*(2-Mathf.Abs(i-2));
                Runtime3DFactory.Box("CrownSpike"+i,_root,new Vector3(x,0.36f+h*0.35f,-0.12f),new Vector3(0.09f,h,0.10f),c,0.70f,0.55f);
            }
        }

        private void BuildZero(Color c, Color dark)
        {
            Runtime3DFactory.Box("ZeroCrossH",_root,new Vector3(0f,0.18f,-0.12f),new Vector3(1.15f,0.16f,0.16f),c,0.72f,0.72f);
            Runtime3DFactory.Box("ZeroCrossV",_root,new Vector3(0f,0.18f,-0.12f),new Vector3(0.16f,1.15f,0.16f),c,0.72f,0.72f);
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI*2f/6f;
                Runtime3DFactory.Cylinder("ZeroNode"+i,_root,new Vector3(Mathf.Cos(a)*0.58f,Mathf.Sin(a)*0.58f,-0.16f),0.16f,0.08f,Color.Lerp(c,Color.white,0.30f),0.42f,0.92f);
            }
        }

        private void LateUpdate()
        {
            if (!_ready || _legend == null || _root == null) return;
            float t=Time.unscaledTime;
            if(_core!=null)
            {
                float p=1f+Mathf.Sin(t*(2.2f+_legend.Phase*0.55f))*0.08f;
                _core.localScale=new Vector3(p,p,1f);
            }
            if(_halo!=null)
            {
                _halo.localRotation=Quaternion.Euler(0f,0f,t*(12f+_tier*2.2f));
                float pulse=1f+Mathf.Sin(t*3.1f)*0.05f;
                _halo.localScale=new Vector3(1.42f*pulse,1.42f*pulse,0.025f);
            }
            float stress=(_legend.Phase-1)*0.012f;
            _root.localPosition=new Vector3(Mathf.Sin(t*7f)*stress,Mathf.Cos(t*6.2f)*stress,-0.72f);
        }
    }
}
