using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class GameMaster : MonoBehaviour
{
    //map data
    private class MapTile
    {
        public bool Bwall = true, BfoodB = true, BfoodC = true, crossing = false, 
            n = false, e = false, s = false, w = false, gate = false, cherry = false;
        public GameObject tile,foodB,foodC;
        public string coord;
    }
    private MapTile[,] map;
    private int offset = 1;
    //materials
    public GameObject TilePrefab, PlayerPrefab, GhostPrefab;
    public Text HUD;
    public int mapIndex = 0, RadiousX = 5, RadiousZ = 5;
    public Material mPAC, mRED, mPIN, mORA, mBLU, mCYA, mDead;
    //game data
    private GameObject pacman, ghostCYA, ghostORA, ghostPIN, ghostRED;
    private Vector3 spawnPACMAN, spawnGHOST;
    private float playerSpeed=4f, ghostSpeed=2f, turnError=0.499f;
    //code for generator
    void Start()
    {
        MakeMap();
        MarkPaths();
        CreateActors();
        UpdateHUDandGenerateRandomEvents();
    }       //done
    void MakeMap()
    {
        transform.position = new Vector3((RadiousX) * offset, transform.position.y, (RadiousZ) * offset);
        string[] source = MapManager.map[mapIndex].Split('\n');
        map = new MapTile[RadiousX + RadiousX + 1, RadiousZ + RadiousZ + 1];
        for (int x = -RadiousX; x <= RadiousX; x++)
            for (int z = -RadiousZ; z <= RadiousZ; z++)
            {
                MapTile newTile = new MapTile();
                {
                    newTile.tile = Instantiate(TilePrefab, new Vector3((x + RadiousX) * offset, 0, (z + RadiousZ) * offset), Quaternion.identity);
                    newTile.foodB = newTile.tile.transform.GetChild(1).gameObject;
                    newTile.foodC = newTile.tile.transform.GetChild(2).gameObject;
                    newTile.coord = x + RadiousX + " " + z + RadiousZ;
                    newTile.tile.name = "PL " + (x + RadiousX) + " " + (z + RadiousZ);
                }
                char type = source[(RadiousZ + RadiousZ) - (z + RadiousZ)].ToCharArray()[x + RadiousX];
                if (type != '#') { Destroy(newTile.tile.transform.GetChild(0).gameObject); newTile.Bwall = false; }//WALL
                if (type != 'o' && type != 'G') { Destroy(newTile.tile.transform.GetChild(1).gameObject); newTile.BfoodB = false; }//FOOD B
                if (type != '.') { Destroy(newTile.tile.transform.GetChild(2).gameObject); newTile.BfoodC = false; }//FOOD C
                if (type == 'S') spawnPACMAN = new Vector3((x + RadiousX) * offset, 0, (z + RadiousZ) * offset);
                if (type == 'G') spawnGHOST  = new Vector3((x + RadiousX) * offset, 0, (z + RadiousZ) * offset);

                if (type == 'G')
                {
                    newTile.BfoodB = false;
                    newTile.cherry = true;
                    newTile.tile.transform.GetChild(1).GetComponent<MeshRenderer>().material = mRED;
                }
                newTile.gate = (type == '-');
                map[x + RadiousX, z + RadiousZ] = newTile;
            }
    }     //done
    void MarkPaths()
    {
        for (int z = 1; z < 2 * RadiousZ; z++) 
            for (int x = 1; x < 2 * RadiousX; x++)
                if (!map[x, z].Bwall)
                {
                    map[x, z].n = !map[x, z + 1].Bwall;
                    map[x, z].e = !map[x + 1, z].Bwall;
                    map[x, z].s = !map[x, z - 1].Bwall;
                    map[x, z].w = !map[x - 1, z].Bwall;
                    int crssZ = 0, crssX = 0;
                    if (map[x, z].n) crssZ++;
                    if (map[x, z].s) crssZ++;
                    
                    if (map[x, z].e) crssX++;
                    if (map[x, z].w) crssX++;

                    if((2<(crssZ + crssX))||(crssZ==crssX))
                        map[x, z].crossing = true;
                    //if there is more than 2 paths to this field OR 
                    //there is turn (if first condition is not met it means that: crssZ=2 crssX=0 or crssZ=0 crssX=2 (with will be 
                    //ignored by this condition OR crssZ==1 crssX==1 thus crssZ==crssX => one comparison less))
                    if (map[x, z].crossing) map[x, z].tile.name += " crossing";
                }
    }   //done
    void CreateActors()
    {
        pacman = Instantiate(PlayerPrefab, spawnPACMAN, Quaternion.identity);
        playerLastHeading = 'E';
        ghostCYA = Instantiate(GhostPrefab, spawnGHOST, Quaternion.identity);
        ghostCYA.GetComponent<MeshRenderer>().material = mCYA;
        ghostORA = Instantiate(GhostPrefab, spawnGHOST, Quaternion.identity);
        ghostORA.GetComponent<MeshRenderer>().material = mORA;
        ghostPIN = Instantiate(GhostPrefab, spawnGHOST, Quaternion.identity);
        ghostPIN.GetComponent<MeshRenderer>().material = mPIN;
        ghostRED = Instantiate(GhostPrefab, spawnGHOST, Quaternion.identity);
        ghostRED.GetComponent<MeshRenderer>().material = mRED;
    }//done
    //code core gameplay
    void Update()
    {
        PlayerControll();
        GhostContoll();
        UpdateHUDandGenerateRandomEvents();
    }      //done
    //player data
    private char playerLastHeading;//N,E,S,W => North, East, South, West <=> Z+, X+, Z-, X-
    private int shore = 0,lifes = 3;
    //PlayerControll is mess, i need to refactor it- and will do it when i will have time.
    void PlayerControll()
    {
        float fX = pacman.transform.position.x, fZ = pacman.transform.position.z;
        int rX = ((int)(fX + 0.5f)), rZ = ((int)(fZ + 0.5f));
        bool onGrid = ((int)(fX + turnError + 0.5f)) == ((int)(fX - turnError + 0.5f)) &&
                      ((int)(fZ + turnError + 0.5f)) == ((int)(fZ - turnError + 0.5f)),
             canTurn = onGrid && map[rX, rZ].crossing;
        char currentHeadingZ = ' ', currentHeadingX = ' ';
        {
             /*
                 To make steering easier controll checks for 2 keys, this way, so long we are not on 
                 crossing we apply key that has clearance OR is firt read here to be checked; this is 
                 done so that when we arrive at crossing game will compare direction we came from and
                 if there is more than one kay pressed (that is perpendicular to the vector we approched 
                 this crossing) game will favor turning rather than going forward.

                 This is direct usage of concepts in terms of controll design from 
                 "Why Celeste feels so good to play" -the game is tring to guess what player actualy 
                 wants rather than watching what he does.
             */
            if (Input.GetKey(KeyCode.W))
                currentHeadingZ = 'N';
            if (Input.GetKey(KeyCode.D))
                currentHeadingX = 'E';
            if (Input.GetKey(KeyCode.S) && currentHeadingZ != 'N')
                currentHeadingZ = 'S';
            if (Input.GetKey(KeyCode.A) && currentHeadingZ != 'E')
                currentHeadingX = 'W';
        }//get key for each axis, favors N and E
        if (!canTurn) {
            if (((playerLastHeading == 'N' || playerLastHeading == 'S') && currentHeadingZ != ' ') ||
                ((playerLastHeading == 'E' || playerLastHeading == 'W') && currentHeadingX != ' ')) {
                if ((playerLastHeading == 'N' || playerLastHeading == 'S') && currentHeadingZ != ' ') {
                    if (currentHeadingZ == 'N') {
                        if (map[rX, rZ].crossing && (fZ < rZ) && (rZ < (fZ + playerSpeed * Time.deltaTime)))
                        {
                            pacman.transform.position = new Vector3
                                (fX, pacman.transform.position.y, rZ);
                        }
                        else
                        {
                            pacman.transform.position = new Vector3
                                (fX, pacman.transform.position.y, fZ + playerSpeed * Time.deltaTime);
                        }
                    }//X+
                    else {
                        if (map[rX, rZ].crossing && (fZ > rZ) && (rZ > (fZ - playerSpeed * Time.deltaTime)))
                        {
                            pacman.transform.position = new Vector3
                                (fX, pacman.transform.position.y, rZ);
                        }
                        else
                        {
                            pacman.transform.position = new Vector3
                                (fX, pacman.transform.position.y, fZ - playerSpeed * Time.deltaTime);
                        }
                    }//X-
                }// axis Z aligned
                else {
                    if (currentHeadingX == 'E') {
                        if (map[rX, rZ].crossing && (fX < rX) && (rX < (fX + playerSpeed * Time.deltaTime)))
                        {
                            pacman.transform.position = new Vector3
                                (rX, pacman.transform.position.y, fZ);
                        }
                        else
                        {
                            pacman.transform.position = new Vector3
                                (fX + playerSpeed * Time.deltaTime, pacman.transform.position.y, fZ);
                        }
                    }//X+
                    else {
                        if (map[rX, rZ].crossing && (fX > rX) && (rX > (fX - playerSpeed * Time.deltaTime)))
                        {
                            pacman.transform.position = new Vector3
                                (rX, pacman.transform.position.y, fZ);
                        }
                        else
                        {
                            pacman.transform.position = new Vector3
                                (fX - playerSpeed * Time.deltaTime, pacman.transform.position.y, fZ);
                        }
                    }//X-
                }// axis X aligned
            }//one of inputs align with current line of movment
        }//movement beyond crossing
        else if(currentHeadingZ!=' '|| currentHeadingX!=' ') {
            pacman.transform.position = new Vector3(rX, pacman.transform.position.y, rZ);//round pacman position for precision
            if((playerLastHeading == 'N' || playerLastHeading == 'S')) {
                if      ((currentHeadingX == 'E' && map[rX, rZ].e) || (currentHeadingX == 'W' && map[rX, rZ].w)) {
                    playerLastHeading = currentHeadingX;
                    if (currentHeadingX == 'E') {
                        pacman.transform.position = new Vector3
                            (fX + (0.5f - turnError + 0.01f), pacman.transform.position.y, fZ);
                    }
                    else{
                        pacman.transform.position = new Vector3
                            (fX - (0.5f - turnError + 0.01f), pacman.transform.position.y, fZ);
                    }
                }//there is perpenticuar input that CAN be executed
                else if ((currentHeadingZ == 'N' && map[rX, rZ].n) || (currentHeadingZ == 'S' && map[rX, rZ].s)) {
                    playerLastHeading = currentHeadingZ;
                    if (currentHeadingZ == 'N')
                    {
                        pacman.transform.position = new Vector3
                            (fX, pacman.transform.position.y, fZ + (0.5f - turnError + 0.01f));
                    }
                    else
                    {
                        pacman.transform.position = new Vector3
                            (fX, pacman.transform.position.y, fZ - (0.5f - turnError + 0.01f));
                    }
                }//there is axis input that CAN be executed
            }//last usedAxis Z
            else {
                if ((currentHeadingZ == 'N' && map[rX, rZ].n) || (currentHeadingZ == 'S' && map[rX, rZ].s)) {
                    playerLastHeading = currentHeadingZ;
                    if (currentHeadingZ == 'N')
                    {
                        pacman.transform.position = new Vector3
                            (fX, pacman.transform.position.y, fZ + playerSpeed * Time.deltaTime);
                    }
                    else
                    {
                        pacman.transform.position = new Vector3
                            (fX, pacman.transform.position.y, fZ - playerSpeed * Time.deltaTime);
                    }
                }//there is axis input that CAN be executed
                else if ((currentHeadingX == 'E' && map[rX, rZ].e) || (currentHeadingX == 'W' && map[rX, rZ].w)) {
                    playerLastHeading = currentHeadingX;
                    if (currentHeadingX == 'E')
                    {
                        pacman.transform.position = new Vector3
                            (fX + playerSpeed * Time.deltaTime, pacman.transform.position.y, fZ);
                    }
                    else
                    {
                        pacman.transform.position = new Vector3
                            (fX - playerSpeed * Time.deltaTime, pacman.transform.position.y, fZ);
                    }
                }//there is perpenticuar input that CAN be executed
            }//last usedAxis X
        }//movement on crossing
        PlayertPickUp();
        //check for collisions
    }   //done
    void PlayertPickUp()
    {
        float fX = pacman.transform.position.x, fZ = pacman.transform.position.z;
        int rX = ((int)(fX + 0.5f)), rZ = ((int)(fZ + 0.5f));
        if (map[rX, rZ].BfoodC)
        {
            shore++;
            map[rX, rZ].BfoodC = false;
            Destroy(map[rX, rZ].tile.transform.GetChild(0).gameObject);
        }
        if (map[rX, rZ].BfoodB)
        {
            shore ++;
            map[rX, rZ].BfoodB = false;
            Destroy(map[rX, rZ].tile.transform.GetChild(0).gameObject);
            fearTimeRemaning = defFearTime;
            MakeAUTurn();
        }
        if (map[rX, rZ].cherry)
        {
            shore+=100;
            map[rX, rZ].cherry = false;
            Destroy(map[rX, rZ].tile.transform.GetChild(0).gameObject);
        }
    }    //done
    public void PlayerGhostColide(Collider other)
    {
        if (other.CompareTag("Enemy-Alive"))
            if (0 < fearTimeRemaning) {
                GameObject ghost = other.gameObject;
                ghost.tag = "Enemy-Dead";
                ghost.GetComponent<MeshRenderer>().material = mDead;
                if (ghost == ghostRED)
                    RedDeadFor = -1;
                if (ghost == ghostPIN)
                    PinDeadFor = -1;
                if (ghost == ghostORA)
                    OraDeadFor = -1;
                if (ghost == ghostCYA)
                    CyaDeadFor = -1;
            } else {
                lifes--;
                pacman.transform.position = spawnPACMAN;
                playerLastHeading = 'E';
                ghostCYA.transform.position = spawnGHOST;
                ghostORA.transform.position = spawnGHOST;
                ghostPIN.transform.position = spawnGHOST;
                ghostRED.transform.position = spawnGHOST;
                RedHeading = 'N';
                PinHeading = 'N';
                OraHeading = 'N';
                CyaHeading = 'N';
                RedDeadFor = 0f;
                PinDeadFor = 1f;
                OraDeadFor = 2f;
                CyaDeadFor = 3f;
                if (lifes<=0)
                    Application.Quit();
            }//done
    }//todo=>
    //ghosts data
    private char  RedHeading = 'N', PinHeading = 'N', OraHeading = 'N', CyaHeading = 'N';
    private float RedDeadFor = 0f, PinDeadFor = 1f, OraDeadFor = 2f, CyaDeadFor = 3f, //if negative ghost is returing to base
                  defDeadTime = 5f, defFearTime = 10f, fearTimeRemaning = 0f;
    void GhostContoll()
    {
        if (fearTimeRemaning == 0)
        {//deadfor < 0 ghost running to cage; 0 < waiting
            if (RedDeadFor == 0) {
                GhostRED(ghostRED);
            } else { DeadGhost(0); }
            if (PinDeadFor == 0) {
                GhostPIN(ghostPIN);
            } else { DeadGhost(1); }
            if (OraDeadFor == 0) {
                GhostORA(ghostORA);
            } else { DeadGhost(2); }
            if (CyaDeadFor == 0) {
                GhostCYA(ghostCYA);
            } else { DeadGhost(3); }
        }
        else
        {
            GhostBlue();
            fearTimeRemaning -= Time.deltaTime;
            if (fearTimeRemaning < 0)
            {
                fearTimeRemaning = 0;
                MakeAUTurn();
            }
        }
    }//done
    void MakeAUTurn()
    {
        switch (RedHeading)
        {
            case 'N': RedHeading = 'S'; break;
            case 'S': RedHeading = 'N'; break;
            case 'E': RedHeading = 'W'; break;
            case 'W': RedHeading = 'E'; break;
        }
        switch (PinHeading)
        {
            case 'N': PinHeading = 'S'; break;
            case 'S': PinHeading = 'N'; break;
            case 'E': PinHeading = 'W'; break;
            case 'W': PinHeading = 'E'; break;
        }
        switch (OraHeading)
        {
            case 'N': OraHeading = 'S'; break;
            case 'S': OraHeading = 'N'; break;
            case 'E': OraHeading = 'W'; break;
            case 'W': OraHeading = 'E'; break;
        }
        switch (CyaHeading)
        {
            case 'N': CyaHeading = 'S'; break;
            case 'S': CyaHeading = 'N'; break;
            case 'E': CyaHeading = 'W'; break;
            case 'W': CyaHeading = 'E'; break;
        }
        if (fearTimeRemaning != 0)
        {
            ghostCYA.GetComponent<MeshRenderer>().material = mBLU;
            ghostORA.GetComponent<MeshRenderer>().material = mBLU;
            ghostPIN.GetComponent<MeshRenderer>().material = mBLU;
            ghostRED.GetComponent<MeshRenderer>().material = mBLU;
        }
        else
        {
            if (CyaDeadFor == 0) ghostCYA.GetComponent<MeshRenderer>().material = mCYA;
            if (OraDeadFor == 0) ghostORA.GetComponent<MeshRenderer>().material = mORA;
            if (PinDeadFor == 0) ghostPIN.GetComponent<MeshRenderer>().material = mPIN;
            if (RedDeadFor == 0) ghostRED.GetComponent<MeshRenderer>().material = mRED;
        }
    }  //done
    //ghost AI
    void GhostRED(GameObject ghost)
    {
        if(MoveGhost(ghost, RedHeading,false))
        {
            Vector3 target = pacman.transform.position;
            RedHeading = CalcNewDirection(ghost.transform.position, target, RedHeading);
        }
    }//done
    void GhostPIN(GameObject ghost)
    {
        if (MoveGhost(ghost, PinHeading, false))
        {
            float x=pacman.transform.position.x, z = pacman.transform.position.z;
            switch (playerLastHeading)
            {
                case 'N': { x -= 4; z += 4; } break;
                case 'E': x += 4; break;
                case 'S': z -= 4; break;
                case 'W': x -= 4; break;
            }
            Vector3 target = new Vector3(x,0,z);
            PinHeading = CalcNewDirection(ghost.transform.position, target, PinHeading);
        }
    }//done
    void GhostCYA(GameObject ghost)
    {
        if (MoveGhost(ghost, CyaHeading, false))
        {
            float x = pacman.transform.position.x, z = pacman.transform.position.z;
            switch (playerLastHeading)
            {
                case 'N': { x -= 2; z += 2; } break;
                case 'E': x += 2; break;
                case 'S': z -= 2; break;
                case 'W': x -= 2; break;
            }
            x = x - (ghostRED.transform.position.x - x);
            z = z - (ghostRED.transform.position.z - z);
            Vector3 target = new Vector3(x, 0, z);
            CyaHeading = CalcNewDirection(ghost.transform.position, target, CyaHeading);
        }
    }//done
    void GhostORA(GameObject ghost)
    {
        if (MoveGhost(ghost, OraHeading, false))
        {
            Vector3 target = pacman.transform.position;
            if (Vector3.Distance(ghost.transform.position, target) < 8)
                target = new Vector3(0,0,-1);
            OraHeading = CalcNewDirection(ghost.transform.position, target, OraHeading);
        }
    }//done
    private int blinkHelper;
    void GhostBlue()
    {
        {
            if (RedDeadFor == 0)
            {
                if (MoveGhost(ghostRED, RedHeading, false))
                {
                    Vector3 target = new Vector3(ghostRED.transform.position.x + Random.Range(-10.0f, 10.0f), 0, ghostRED.transform.position.z + Random.Range(-10.0f, 10.0f));
                    RedHeading = CalcNewDirection(ghostRED.transform.position, target, RedHeading);
                }
            }
            else
            {
                DeadGhost(0);
            }
            if (PinDeadFor == 0)
            {
                if (MoveGhost(ghostPIN, PinHeading, false))
                {
                    Vector3 target = new Vector3(ghostPIN.transform.position.x + Random.Range(-10.0f, 10.0f), 0, ghostPIN.transform.position.z + Random.Range(-10.0f, 10.0f));
                    PinHeading = CalcNewDirection(ghostPIN.transform.position, target, PinHeading);
                }
            }
            else
            {
                DeadGhost(1);
            }
            if (OraDeadFor == 0)
            {
                if (MoveGhost(ghostORA, OraHeading, false))
                {
                    Vector3 target = new Vector3(ghostORA.transform.position.x + Random.Range(-10.0f, 10.0f), 0, ghostORA.transform.position.z + Random.Range(-10.0f, 10.0f));
                    OraHeading = CalcNewDirection(ghostORA.transform.position, target, OraHeading);
                }
            }
            else
            {
                DeadGhost(2);
            }
            if (CyaDeadFor == 0)
            {
                if (MoveGhost(ghostCYA, CyaHeading, false))
                {
                    Vector3 target = new Vector3(ghostCYA.transform.position.x + Random.Range(-10.0f, 10.0f), 0, ghostCYA.transform.position.z + Random.Range(-10.0f, 10.0f));
                    CyaHeading = CalcNewDirection(ghostCYA.transform.position, target, CyaHeading);
                }
            }
            else
            {
                DeadGhost(3);
            }
        }
        float t = fearTimeRemaning;
        if (0.5f<t&&t < 2)
        {
            if (blinkHelper == 0) {
                BlinkGhosts();
            } else {
                if (blinkHelper == 5)
                    blinkHelper = -1;
            }
            blinkHelper++;
        }
        else if(t < 0.5f)
        {
            if (RedDeadFor == 0) ghostRED.SetActive(true);
            if (PinDeadFor == 0) ghostPIN.SetActive(true);
            if (OraDeadFor == 0) ghostORA.SetActive(true);
            if (CyaDeadFor == 0) ghostCYA.SetActive(true);
        }
    }  //done
    void BlinkGhosts()
    {
        if (RedDeadFor == 0) ghostRED.SetActive(!ghostRED.active);
        if (PinDeadFor == 0) ghostPIN.SetActive(!ghostPIN.active);
        if (OraDeadFor == 0) ghostORA.SetActive(!ghostORA.active);
        if (CyaDeadFor == 0) ghostCYA.SetActive(!ghostCYA.active);
    }//done
    //red,pin,ora,cya =? 0,1,2,3
    void DeadGhost(int GhostNo)
    {
        float deadFor = 0;
        GameObject ghost=null;
        switch (GhostNo)
        {
            case 0: {
                    deadFor = RedDeadFor;
                    ghost = ghostRED;
                } break;
            case 1: {
                    deadFor = PinDeadFor;
                    ghost = ghostPIN;
                } break;
            case 2: {
                    deadFor = OraDeadFor;
                    ghost = ghostORA;
                } break;
            case 3: {
                    deadFor = CyaDeadFor;
                    ghost = ghostCYA;
                } break;
        }
        if (deadFor < 0)
        {
            switch (GhostNo)
            {
                case 0:
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float fX = ghost.transform.position.x, fZ = ghost.transform.position.z;
                            int rX = ((int)(fX + 0.5f)), rZ = ((int)(fZ + 0.5f));
                            if (MoveGhost(ghost, RedHeading, true)) {
                                RedHeading = CalcNewDirection(ghost.transform.position, spawnGHOST, RedHeading);
                            }
                            if (spawnGHOST.x == rX && spawnGHOST.z == rZ)
                            {
                                    deadFor = defDeadTime;
                                    RedHeading = 'N';
                            }
                        }
                    }
                    break;
                case 1:
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float fX = ghost.transform.position.x, fZ = ghost.transform.position.z;
                            int rX = ((int)(fX + 0.5f)), rZ = ((int)(fZ + 0.5f));
                            if (MoveGhost(ghost, PinHeading, true))
                            {
                                PinHeading = CalcNewDirection(ghost.transform.position, spawnGHOST, PinHeading);
                            }
                            if (spawnGHOST.x == rX && spawnGHOST.z == rZ)
                            {
                                deadFor = defDeadTime;
                                PinHeading = 'N';
                            }
                        }
                    }
                    break;
                case 2:
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float fX = ghost.transform.position.x, fZ = ghost.transform.position.z;
                            int rX = ((int)(fX + 0.5f)), rZ = ((int)(fZ + 0.5f));
                            if (MoveGhost(ghost, OraHeading, true))
                            {
                                OraHeading = CalcNewDirection(ghost.transform.position, spawnGHOST, OraHeading);
                            }
                            if (spawnGHOST.x == rX && spawnGHOST.z == rZ)
                            {
                                    deadFor = defDeadTime;
                                    OraHeading = 'N';
                            }
                            
                        }
                    }
                    break;
                case 3:
                    {
                        for (int i = 0; i < 3; i++) {
                            float fX = ghost.transform.position.x, fZ = ghost.transform.position.z;
                            int rX = ((int)(fX + 0.5f)), rZ = ((int)(fZ + 0.5f));
                            if (MoveGhost(ghost, CyaHeading, true))
                        {
                            CyaHeading = CalcNewDirection(ghost.transform.position, spawnGHOST, CyaHeading);
                            
                        }
                            if (spawnGHOST.x == rX && spawnGHOST.z == rZ)
                            {
                                deadFor = defDeadTime;
                                CyaHeading = 'N';
                            }
                        }
                    }
                    break;
            }
        }//navigate ghost to cage
        else {
            deadFor -= Time.deltaTime;
            if (deadFor < 0)
            {
                deadFor = 0;
                ghost.tag = "Enemy-Alive";
                switch (GhostNo)
                {
                    case 0:
                        {
                            ghostRED.GetComponent<MeshRenderer>().material = mRED;
                            RedHeading = 'N';
                        }
                        break;
                    case 1:
                        {
                            ghostPIN.GetComponent<MeshRenderer>().material = mPIN;
                            PinHeading = 'N';
                        }
                        break;
                    case 2:
                        {
                            ghostORA.GetComponent<MeshRenderer>().material = mORA;
                            OraHeading = 'N';
                        }
                        break;
                    case 3:
                        {
                            ghostCYA.GetComponent<MeshRenderer>().material = mCYA;
                            CyaHeading = 'N';
                        }
                        break;
                }
            }
        }//wait for spawn
        switch (GhostNo)
        {
            case 0:
                {
                    RedDeadFor = deadFor;
                }
                break;
            case 1:
                {
                    PinDeadFor = deadFor;
                }
                break;
            case 2:
                {
                    OraDeadFor = deadFor;
                }
                break;
            case 3:
                {
                    CyaDeadFor = deadFor;
                }
                break;
        }
    }
    //end of ghost AI
    bool MoveGhost(GameObject ghost, char direction,bool dead)
    {
        float fX = ghost.transform.position.x, fZ = ghost.transform.position.z;
        int rX = ((int)(fX + 0.5f)), rZ = ((int)(fZ + 0.5f));
        bool onGrid = ((int)(fX + turnError + 0.5f)) == ((int)(fX - turnError + 0.5f)) &&
                      ((int)(fZ + turnError + 0.5f)) == ((int)(fZ - turnError + 0.5f)),

             canTurn = (onGrid && map[rX, rZ].crossing) &&
((!((((rX == 12 && rZ == 7) || (rX == 15 && rZ == 7)) || ((rX == 12 && rZ == 19) || (rX == 15 && rZ == 19))) && direction!='S'))&&
//lista pól na których nie można skręcić I pytanie czy nie idziemy na połudznie, jeżeli tak, musimy skręcić.
(!((rX == 13 && rZ == 19)&&((!dead)&&direction!='N'))));
//czy jestem przed bramą i czy jestem martwy aby tam wejść        
        switch (direction)
        {
            case 'N':
                {
                    if (map[rX, rZ].crossing && (fZ < rZ) && (rZ < (fZ + ghostSpeed * Time.deltaTime)))
                    {
                        ghost.transform.position = new Vector3
                            (rX, ghost.transform.position.y, rZ);
                    }
                    else
                    {
                        ghost.transform.position = new Vector3
                            (rX, ghost.transform.position.y, fZ + ghostSpeed * Time.deltaTime);
                    }
                }
                break;
            case 'S':
                {
                    if (map[rX, rZ].crossing && (fZ > rZ) && (rZ > (fZ - ghostSpeed * Time.deltaTime)))
                    {
                        ghost.transform.position = new Vector3
                            (rX, ghost.transform.position.y, rZ);
                    }
                    else
                    {
                        ghost.transform.position = new Vector3
                            (rX, ghost.transform.position.y, fZ - ghostSpeed * Time.deltaTime);
                    }
                }
                break;
            case 'E':
                {
                    if (map[rX, rZ].crossing && (fX < rX) && (rX < (fX + ghostSpeed * Time.deltaTime)))
                    {
                        ghost.transform.position = new Vector3
                            (rX, ghost.transform.position.y, rZ);
                    }
                    else
                    {
                        ghost.transform.position = new Vector3
                            (fX + ghostSpeed * Time.deltaTime, ghost.transform.position.y, rZ);
                    }
                }
                break;
            case 'W':
                {
                    if (map[rX, rZ].crossing && (fX > rX) && (rX > (fX - ghostSpeed * Time.deltaTime)))
                    {
                        ghost.transform.position = new Vector3
                            (rX, ghost.transform.position.y, rZ);
                    }
                    else
                    {
                        ghost.transform.position = new Vector3
                            (fX - ghostSpeed * Time.deltaTime, ghost.transform.position.y, rZ);
                    }
                }
                break;
        }//movement beyond crossing
        {// check for exceptions

        }
        return canTurn;
    }           //done => todo special cases
    char CalcNewDirection(Vector3 origin, Vector3 target, char direction)
    {
        float fX = origin.x, fZ = origin.z;
        int rX = ((int)(fX + 0.5f)), rZ = ((int)(fZ + 0.5f));
        Vector3 N=new Vector3(-1000,-1000,-1000), E=N, S=N, W=N;
        if (!map[rX, rZ + 1].Bwall && direction != 'S')
            N = map[rX, rZ + 1].tile.transform.position;
        if (!map[rX + 1, rZ].Bwall && direction != 'W')
            E = map[rX + 1, rZ].tile.transform.position;
        if (!map[rX, rZ - 1].Bwall && direction != 'N')
            S = map[rX, rZ - 1].tile.transform.position;
        if (!map[rX - 1, rZ].Bwall && direction != 'E')
            W = map[rX - 1, rZ].tile.transform.position;

        float distToTarget=1000,
            distN = Vector3.Distance(N, target),
            distE = Vector3.Distance(E, target),
            distS = Vector3.Distance(S, target),
            distW = Vector3.Distance(W, target);
        char currentBest = ' ';
        
        if (distN < distToTarget) { distToTarget = distN; currentBest = 'N'; }
        if (distE < distToTarget) { distToTarget = distE; currentBest = 'E'; }
        if (distS < distToTarget) { distToTarget = distS; currentBest = 'S'; }
        if (distW < distToTarget) { distToTarget = distW; currentBest = 'W'; }

        if (distE == distToTarget) currentBest = 'E';
        if (distS == distToTarget) currentBest = 'S';
        if (distW == distToTarget) currentBest = 'W';
        if (distN == distToTarget) currentBest = 'N';
        return currentBest;
    }//done
    //hud data
    void UpdateHUDandGenerateRandomEvents()
    {
        HUD.text =
            "WORK IN PROGRESS; SHORE:"+shore+"; Lifes:"+lifes;
    }//done
}
/*
DONE - 5pts. Pacman’s movement is different to Sokoban in that it can stop, or reverse direction at any moment -- it does not need to move all the way to the next grid cell in order to react to player input. However when it moves along a corridor, it can’t change direction sideways, of course. When coming to crossings (grid cells with 3 or 4 open direction) the keys/control scheme must be easy for the player to change direction of movement.
DONE - 5pts. Implement ghost movement for the ‘chase’ state of the game, where each ghost has a different way of computing the target point (as explained in the video), and then at moments when they are at an intersection they use a simple rule to choose new movement direction.
DONE - 5pts. Implement all the dots to pickup, and start the game with ghost staying in the center ‘cage’, and leaving the cage one at a time. Keep track of points, and drop a cherry or other fruit in the center for extra points.
DONE - 5pts. Implement the frightened mode of the game, when ghosts run away from player, after the player picks up a big dot. Use a set duration for that phase, and blink ghosts soon before the game mode switches back to chase.
DONE - 5pts. Implement events when a ghost catches the player, (or the other way around when in ‘frightened’ mode). Implement player losing a life & resuming game afterwards or going to game over screen. In frightened mode, Implement the ghost entering the ‘eyes’ mode & moving back to the center cage, from which it will exit after some time.
*/
