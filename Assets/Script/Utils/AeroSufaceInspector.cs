using AeroSim.AeroPhysics;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

namespace AeroSim.Utils
{
    public class AeroSufaceInspector : MonoBehaviour
    {
        public AeroPhysics.AeroSurface surface;
        public Material lineMaterial;
        public Vector2 position;
        public Vector2 scale;
        //public float deltaX = 100;

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        void OnGUI()
        {
            GL.PushMatrix();
            GL.LoadOrtho();

            lineMaterial.SetPass(0);

            GL.Begin(GL.LINES);
            LDMCoefficients lastCoeff = new();

            //axis
            GL.Vertex3(position.x - 180 * scale.x, position.y, 0);
            GL.Vertex3(position.x + 180 * scale.x, position.y, 0);
            GL.Vertex3(position.x, position.y - scale.y, 0);
            GL.Vertex3(position.x, position.y + scale.y, 0);

            for (int aoa = -180; aoa < 180; aoa++)
            {
                LDMCoefficients coeff = surface.CalcucateCoiffients(aoa);
                if(aoa == -180)
                {
                    lastCoeff = coeff;
                    continue;
                }

                //coeff - aoa image
                GL.Color(Color.green);
                GL.Vertex3((aoa - 1) * scale.x + position.x, lastCoeff.liftCoefficient * scale.y + position.y, 0);
                GL.Vertex3(aoa * scale.x + position.x, coeff.liftCoefficient * scale.y + position.y, 0);

                GL.Color(Color.red);
                GL.Vertex3((aoa - 1) * scale.x + position.x, lastCoeff.dragCoefficient * scale.y + position.y, 0);
                GL.Vertex3(aoa * scale.x + position.x, coeff.dragCoefficient * scale.y + position.y, 0);

                GL.Color(Color.blue);
                GL.Vertex3((aoa - 1) * scale.x + position.x, lastCoeff.torqueCoefficient * scale.y + position.y, 0);
                GL.Vertex3(aoa * scale.x + position.x, coeff.torqueCoefficient * scale.y + position.y, 0);

                //current aoa
                GL.Color(Color.white);
                GL.Vertex3(Aircraft.main.AOA * scale.x + position.x, position.y - scale.y, 0);
                GL.Vertex3(Aircraft.main.AOA * scale.x + position.x, position.y + scale.y, 0);

                //axis
                //for (int i = 0; i < 3; i++)
                //{
                //    GL.Vertex3(i * deltaX + position.x - 180 * scale.x, position.y, 0);
                //    GL.Vertex3(i * deltaX + position.x + 180 * scale.x, position.y, 0);
                //    GL.Vertex3(i * deltaX + position.x, position.y - scale.y, 0);
                //    GL.Vertex3(i * deltaX + position.x, position.y + scale.y, 0);
                //}

                //GL.Vertex3((aoa - 1) * scale.x + position.x, lastCoeff.liftCoefficient * scale.y + position.y, 0);
                //GL.Vertex3(aoa * scale.x + position.x, coeff.liftCoefficient * scale.y + position.y, 0);

                //GL.Vertex3((aoa - 1) * scale.x + position.x + deltaX, lastCoeff.dragCoefficient * scale.y + position.y, 0);
                //GL.Vertex3(aoa * scale.x + position.x + deltaX, coeff.dragCoefficient * scale.y + position.y, 0);

                //GL.Vertex3((aoa - 1) * scale.x + position.x + 2 * deltaX, lastCoeff.torqueCoefficient * scale.y + position.y, 0);
                //GL.Vertex3(aoa * scale.x + position.x + 2 * deltaX, coeff.torqueCoefficient * scale.y + position.y, 0);

                //current aoa
                //for (int i = 0; i < 3; i++)
                //{
                //    GL.Vertex3(i * deltaX + Aircraft.main.AOA * scale.x + position.x, position.y - scale.y, 0);
                //    GL.Vertex3(i * deltaX + Aircraft.main.AOA * scale.x + position.x, position.y + scale.y, 0);
                //}

                lastCoeff = coeff;
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}