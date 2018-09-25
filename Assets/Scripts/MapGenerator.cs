using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class MapGenerator : MonoBehaviour {

	public int width;
	public int height;
	public int borderSize;
	int totalNumCells;

	public string seed;
	public bool useRandomSeed;

	public int smoothIts;

	[Range(0, 100)]
	public int randomFillPercent;

	public int connectionRadius;

	int[,] map; // 0 = empty, 1 = wall

	private void Start() {
		GenerateMap();
	}

	private void Update() {
		if(Input.GetKeyDown(KeyCode.Space)) {
			GenerateMap();
		}
	}

	void GenerateMap() {
		map = new int[width, height];
		totalNumCells = width * height;
		RandomFillMap();

		for(int i = 0; i < smoothIts; i++) {
			SmoothMap();
		}

		RemoveAbberations();

		int[,] borderedMap = new int[width + borderSize * 2, height + borderSize * 2];

		for (int x = 0; x < borderedMap.GetLength(0); x++) {
			for (int y = 0; y < borderedMap.GetLength(1); y++) {
				if(x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize) {
					borderedMap[x, y] = map[x - borderSize, y - borderSize];
				} else {
					borderedMap[x, y] = 1;
				}
			}
		}

		MeshGenerator meshGen = GetComponent<MeshGenerator>();

		meshGen.GenerateMesh(borderedMap, 1);
	}

	void RemoveAbberations() {
		int wallThresholdSize = 50;
		List<List<Coord>> wallRegions = GetRegions(1);
		foreach(List<Coord> wallRegion in wallRegions) {
			if(wallRegion.Count < wallThresholdSize) {
				foreach(Coord tile in wallRegion) {
					map[tile.tileX, tile.tileY] = 0;
				}
			}
		}

		int roomThresholdSize = 50;
		List<List<Coord>> roomRegions = GetRegions(0);
		List<Room> survivingRooms = new List<Room>();
		foreach(List<Coord> roomRegion in roomRegions) {
			if(roomRegion.Count < roomThresholdSize) {
				foreach(Coord tile in roomRegion) {
					map[tile.tileX, tile.tileY] = 1;
				}
			} else {
				survivingRooms.Add(new Room(roomRegion, map));
			}
		}
		survivingRooms.Sort();
		survivingRooms[0].isMainRoom = true;
		survivingRooms[0].isAccessibleFromMainRoom = true;

		ConnectClosestRooms(survivingRooms);
	}

	void ConnectClosestRooms(List<Room> allRooms, bool forceAccessibilityFromMainRoom = false) {

		List<Room> roomListA = new List<Room>();
		List<Room> roomListB = new List<Room>();
		
		if(forceAccessibilityFromMainRoom) {
			foreach(Room room in allRooms) {
				if(room.isAccessibleFromMainRoom) {
					roomListB.Add(room);
				} else {
					roomListA.Add(room);
				}
			}
		} else {
			roomListA = allRooms;
			roomListB = allRooms;
		}

		int bestDistance = 0;
		Coord bestTileA = new Coord();
		Coord bestTileB = new Coord();
		Room bestRoomA = new Room();
		Room bestRoomB = new Room();
		bool possibleConnectionFound = false;

		foreach(Room roomA in roomListA) {
			if(!forceAccessibilityFromMainRoom) {
				possibleConnectionFound = false;
				if(roomA.connectedRooms.Count > 0) continue;
			}

			foreach(Room roomB in roomListB) {
				if(roomA == roomB || roomA.IsConnected(roomB)) continue;
		
				for(int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++) {
					for(int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++) {
						Coord tileA = roomA.edgeTiles[tileIndexA];
						Coord tileB = roomB.edgeTiles[tileIndexB];

						int squaredDistBetweenRooms = (int)(Mathf.Pow(tileA.tileX - tileB.tileX, 2) + Mathf.Pow(tileA.tileY - tileB.tileY, 2));
						if(squaredDistBetweenRooms < bestDistance || !possibleConnectionFound) {
							bestDistance = squaredDistBetweenRooms;
							possibleConnectionFound = true;
							bestTileA = tileA;
							bestTileB = tileB;
							bestRoomA = roomA;
							bestRoomB = roomB;
						}

					}
				}
			}
			if(possibleConnectionFound && !forceAccessibilityFromMainRoom) {
				CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			}
		}

		if(possibleConnectionFound && forceAccessibilityFromMainRoom) {
			CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			ConnectClosestRooms(allRooms, true);
		}

		if(!forceAccessibilityFromMainRoom) {
			ConnectClosestRooms(allRooms, true);
		}
	}

	void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB) {
		Room.ConnectRooms(roomA, roomB);

		List<Coord> line = GetLine(tileA, tileB);
		foreach(Coord c in line) {
			DrawCircle(c, connectionRadius);
		}
	}

	void DrawCircle(Coord c, int r) {
		for(int x = -r; x <= r; x++) {
			for(int y = -r; y <= r; y++) {
				if(x*x + y*y <= r*r) {
					int drawX = c.tileX + x;
					int drawY = c.tileY + y;
					if(IsWithinMap(drawX, drawY)) {
						map[drawX, drawY] = 0;
					}
				}
			}
		}
	}

	List<Coord> GetLine(Coord from, Coord to) {
		List<Coord> line = new List<Coord>();

		int x = from.tileX;
		int y = from.tileY;

		int dx = to.tileX - from.tileX;
		int dy = to.tileY - from.tileY;

		bool inverted = false;
		int step = Math.Sign(dx);
		int gradientStep = Math.Sign(dy);

		int longest = Mathf.Abs(dx);
		int shortest = Mathf.Abs(dy);

		if(longest < shortest) {
			inverted = true;
			longest = Mathf.Abs(dy);
			shortest = Mathf.Abs(dx);

			step = Math.Sign(dy);
			gradientStep = Math.Sign(dx);
		}

		int gradientAccumulation = longest / 2;
		for(int i = 0; i < longest; i++) {
			line.Add(new Coord(x, y));

			if(inverted) {
				y += step;
			} else {
				x += step;
			}

			gradientAccumulation += shortest;
			if(gradientAccumulation >= longest) {
				if(inverted) {
					x += gradientStep;
				} else {
					y += gradientStep;
				}
				gradientAccumulation -= longest;
			}
		}

		return line;
	}

	Vector3 CoordToWorldPoint(Coord tile) {
		return new Vector3(-width / 2 + .5f + tile.tileX, 2, -height / 2 + .5f + tile.tileY);
	}

	List<List<Coord>> GetRegions(int tileType) {
		List<List<Coord>> regions = new List<List<Coord>>();
		int[,] mapCheckFlags = new int[width, height];
		for(int x = 0; x < width; x++) {
			for(int y = 0; y < height; y++) {
				if(mapCheckFlags[x, y] == 0 && map[x, y] == tileType) {
					List<Coord> newRegion = GetRegionTiles(x, y);
					regions.Add(newRegion);

					foreach(Coord tile in newRegion) {
						mapCheckFlags[tile.tileX, tile.tileY] = 1;
					}
				}
			}
		}
		return regions;
	}

	List<Coord> GetRegionTiles(int startX, int startY) {
		List<Coord> tiles = new List<Coord>();
		int[,] mapCheckFlags = new int[width, height];
		int tileType = map[startX, startY];

		Queue<Coord> queue = new Queue<Coord>();
		queue.Enqueue(new Coord(startX, startY));
		mapCheckFlags[startX, startY] = 1;

		while(queue.Count > 0) {
			Coord tile = queue.Dequeue();
			tiles.Add(tile);

			for(int x = tile.tileX - 1; x <= tile.tileX + 1; x++) {
				for(int y = tile.tileY - 1; y <= tile.tileY + 1; y++) {
					if(IsWithinMap(x, y) && (y == tile.tileY || x == tile.tileX)) {
						if(mapCheckFlags[x, y] == 0 && map[x, y] == tileType) {
							mapCheckFlags[x, y] = 1;
							queue.Enqueue(new Coord(x, y));
						}
					}
				}
			}
		}
		return tiles;
	}

	bool IsWithinMap(int x, int y) {
		return x >= 0 && x < width && y >= 0 && y < height;
	}

	void RandomFillMap() {
		if(useRandomSeed) {
			seed = Time.time.ToString();
		}

		System.Random pseudoRandom = new System.Random(seed.GetHashCode());

		for(int x = 0; x < width; x++) {
			for (int y = 0; y < height; y++) {
				if (x == 0 || x == width - 1 || y == 0 || y == height - 1) {
					map[x, y] = 1;
				} else {
					map[x, y] = (pseudoRandom.Next(0, 100) < randomFillPercent) ? 1 : 0;
				}
			}
		}
	}

	void SmoothMap() {
		int[,] tempMap = (int[,])map.Clone();
		for (int x = 0; x < width; x++) {
			for (int y = 0; y < height; y++) {
				int neighborWallTiles = getSurroundingWallCount(x, y);

				if(neighborWallTiles > 4) {
					tempMap[x, y] = 1;
				} else if (neighborWallTiles < 4) {
					tempMap[x, y] = 0;
				} else {
					tempMap[x, y] = map[x, y];
				}
			}
		}
		map = (int[,])tempMap.Clone();
	}

	int getSurroundingWallCount(int x, int y) {
		int count = 0;
		for (int neighborX = x - 1; neighborX <= x + 1; neighborX++) {
			for (int neighborY = y - 1; neighborY <= y + 1; neighborY++) {
				if (IsWithinMap(neighborX, neighborY)) {
					if (neighborX != x || neighborY != y) {
						count += map[neighborX, neighborY];
					}
				} else count++;
			} 
		}
		return count;
	}

	struct Coord {
		public int tileX;
		public int tileY;

		public Coord(int x, int y) {
			tileX = x;
			tileY = y;
		}
	}

	class Room : IComparable<Room> {
		public List<Coord> tiles;
		public List<Coord> edgeTiles;
		public List<Room> connectedRooms;
		public int roomSize;
		public bool isAccessibleFromMainRoom;
		public bool isMainRoom;

		public Room() {}

		public Room(List<Coord> roomTiles, int[,] map) {
			tiles = roomTiles;
			roomSize = tiles.Count;
			connectedRooms = new List<Room>();
			edgeTiles = new List<Coord>();

			foreach(Coord tile in tiles) {
				for(int x = tile.tileX - 1; x <= tile.tileX + 1; x++) {
					for(int y = tile.tileY - 1; y <= tile.tileY + 1; y++) {
						if(x == tile.tileX || y == tile.tileY) {
							if(map[x, y] == 1) {
								edgeTiles.Add(tile);
							}
						}
					}
				}
			}
		}

		public void SetAccessibleFromMainRoom() {
			if(!isAccessibleFromMainRoom) {
				isAccessibleFromMainRoom = true;
				foreach(Room connectedRoom in connectedRooms) {
					connectedRoom.SetAccessibleFromMainRoom();
				}
			}
		}

		public static void ConnectRooms(Room roomA, Room roomB) {
			if(roomA.isAccessibleFromMainRoom) {
				roomB.SetAccessibleFromMainRoom();
			} else if(roomB.isAccessibleFromMainRoom) {
				roomA.SetAccessibleFromMainRoom();
			}
			roomA.connectedRooms.Add(roomB);
			roomB.connectedRooms.Add(roomA);
		}

		public bool IsConnected(Room otherRoom) {
			return connectedRooms.Contains(otherRoom);
		}

		public int CompareTo(Room other) {
			return other.roomSize.CompareTo(roomSize);
		}
	}
}
