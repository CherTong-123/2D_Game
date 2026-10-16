using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class maincell : MonoBehaviour
{
    public int hp = 100,cost=100;
    public enum cellsize { size1=1,size2=2,size3=4,size4=6,size5=9}
    public enum pmt { None=0,Weak=1,Strong=2}
    public bool attach,divity,link=true,eat;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
