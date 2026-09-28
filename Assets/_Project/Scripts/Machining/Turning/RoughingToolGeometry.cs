using UnityEngine;
namespace LastMachinist.Machining.Turning
{
    public static class RoughingToolGeometry
    {
        public const float UnitsPerMm=.05f;
        public static Mesh Build(RoughingToolState tool)
        {
            float a=tool.EdgeStart, b=tool.EdgeEnd;
            var mesh=new Mesh { name="Parallelogram roughing edge" };
            mesh.vertices=new[]{new Vector3(a,0,-2),new Vector3(b,0,-2),new Vector3(b+6,15,-2),new Vector3(a+6,15,-2),
                new Vector3(a,0,2),new Vector3(b,0,2),new Vector3(b+6,15,2),new Vector3(a+6,15,2)};
            var vertices=mesh.vertices; for(int i=0;i<vertices.Length;i++) vertices[i]*=UnitsPerMm; mesh.vertices=vertices;
            mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
