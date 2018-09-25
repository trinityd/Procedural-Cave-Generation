using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour {

	public SquareGrid squareGrid;
	public MeshFilter walls;
	public MeshFilter cave;

	public bool is2D;

	List<Vector3> vertices;
	List<int> triangles;

	Dictionary<int, List<Triangle>> triangleDict = new Dictionary<int, List<Triangle>>();
	List<List<int>> outlines = new List<List<int>>();
	HashSet<int> checkedVertices = new HashSet<int>();

	public void GenerateMesh(int[,] map, float squareSize) {

		triangleDict.Clear();
		outlines.Clear();
		checkedVertices.Clear();

		squareGrid = new SquareGrid(map, squareSize);

		vertices = new List<Vector3>();
		triangles = new List<int>();

		for (int x = 0; x < squareGrid.squares.GetLength(0); x++) {
			for (int y = 0; y < squareGrid.squares.GetLength(1); y++) {
				TriangulateSquare(squareGrid.squares[x, y]);
			}
		}

		Mesh mesh = new Mesh();
		cave.mesh = mesh;

		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.ToArray();
		mesh.RecalculateNormals();

		int tileAmount = 10;
		Vector2[] uvs = new Vector2[vertices.Count];
		for(int i = 0; i < vertices.Count; i++) {
			float percentX = Mathf.InverseLerp(-map.GetLength(0) / 2 * squareSize, map.GetLength(0) / 2 * squareSize, vertices[i].x) * tileAmount;
			float percentY = Mathf.InverseLerp(-map.GetLength(0) / 2 * squareSize, map.GetLength(0) / 2 * squareSize, vertices[i].z) * tileAmount;
			uvs[i] = new Vector2(percentX, percentY);
		}
		mesh.uv = uvs;

		if(is2D) {
			Generate2DColliders();
		} else {
			CreateWallMesh();
		}
	}

	void CreateWallMesh() {
		CalculateMeshOutlines();

		List<Vector3> wallVertices = new List<Vector3>();
		List<int> wallTriangles = new List<int>();
		Mesh wallMesh = new Mesh();
		float wallHeight = 5;

		foreach(List<int> outline in outlines) {
			for(int i = 0; i < outline.Count-1; i++) {
				int startIndex = wallVertices.Count;
				wallVertices.Add(vertices[outline[i]]); // Left
				wallVertices.Add(vertices[outline[i+1]]); // Right
				wallVertices.Add(vertices[outline[i]] - Vector3.up * wallHeight); // Bot Left
				wallVertices.Add(vertices[outline[i + 1]] - Vector3.up * wallHeight); // Bot Right

				wallTriangles.Add(startIndex + 0);
				wallTriangles.Add(startIndex + 2);
				wallTriangles.Add(startIndex + 3);

				wallTriangles.Add(startIndex + 3);
				wallTriangles.Add(startIndex + 1);
				wallTriangles.Add(startIndex + 0);
			}
		}
		wallMesh.vertices = wallVertices.ToArray();
		wallMesh.triangles = wallTriangles.ToArray();
		walls.mesh = wallMesh;

		MeshCollider wallCollider = walls.gameObject.GetComponent<MeshCollider>();
		wallCollider.sharedMesh = wallMesh;
	}

	void Generate2DColliders() {
		EdgeCollider2D[] currentColliders = gameObject.GetComponents<EdgeCollider2D>();
		for(int i = 0; i < currentColliders.Length; i++) {
			Destroy(currentColliders[i]);
		}

		CalculateMeshOutlines();

		foreach(List<int> outline in outlines) {
			EdgeCollider2D edgeCollider = gameObject.AddComponent<EdgeCollider2D>();
			Vector2[] edgePoints = new Vector2[outline.Count];

			for(int i = 0; i < outline.Count; i++) {
				edgePoints[i] = new Vector2(vertices[outline[i]].x, vertices[outline[i]].z);
			}

			edgeCollider.points = edgePoints;
		}
	}

	void TriangulateSquare(Square square) {
		switch(square.configuration) {
			case 0:
				break;

			// 1 points:
			case 1:
				MeshFromPoints(square.leftMid, square.botMid, square.botLeft);
				break;
			case 2:
				MeshFromPoints(square.botRight, square.botMid, square.rightMid);
				break;
			case 4:
				MeshFromPoints(square.topRight, square.rightMid, square.topMid);
				break;
			case 8:
				MeshFromPoints(square.topLeft, square.topMid, square.leftMid);
				break;

			// 2 points:
			case 3:
				MeshFromPoints(square.rightMid, square.botRight, square.botLeft, square.leftMid);
				break;
			case 6:
				MeshFromPoints(square.topMid, square.topRight, square.botRight, square.botMid);
				break;
			case 9:
				MeshFromPoints(square.topLeft, square.topMid, square.botMid, square.botLeft);
				break;
			case 12:
				MeshFromPoints(square.topLeft, square.topRight, square.rightMid, square.leftMid);
				break;
			case 5:
				MeshFromPoints(square.topMid, square.topRight, square.rightMid, square.botMid, square.botLeft, square.leftMid);
				break;
			case 10:
				MeshFromPoints(square.topLeft, square.topMid, square.rightMid, square.botRight, square.botMid, square.leftMid);
				break;

			// 3 point:
			case 7:
				MeshFromPoints(square.topMid, square.topRight, square.botRight, square.botLeft, square.leftMid);
				break;
			case 11:
				MeshFromPoints(square.topLeft, square.topMid, square.rightMid, square.botRight, square.botLeft);
				break;
			case 13:
				MeshFromPoints(square.topLeft, square.topRight, square.rightMid, square.botMid, square.botLeft);
				break;
			case 14:
				MeshFromPoints(square.topLeft, square.topRight, square.botRight, square.botMid, square.leftMid);
				break;

			// 4 point:
			case 15:
				MeshFromPoints(square.topLeft, square.topRight, square.botRight, square.botLeft);
				checkedVertices.Add(square.topLeft.vertexIndex);
				checkedVertices.Add(square.topRight.vertexIndex);
				checkedVertices.Add(square.botLeft.vertexIndex);
				checkedVertices.Add(square.botRight.vertexIndex);
				break;
		}
	}

	void MeshFromPoints(params Node[] points) {
		AssignVertices(points);
		
		if(points.Length >= 3) {
			CreateTriangle(points[0], points[1], points[2]);
		}
		if(points.Length >= 4) {
			CreateTriangle(points[0], points[2], points[3]);
		}
		if (points.Length >= 5) {
			CreateTriangle(points[0], points[3], points[4]);
		}
		if (points.Length >= 6) {
			CreateTriangle(points[0], points[4], points[5]);
		}
	}

	void AssignVertices(Node[] points) {
		for(int i = 0; i < points.Length; i++) {
			if(points[i].vertexIndex == -1) {
				points[i].vertexIndex = vertices.Count;
				vertices.Add(points[i].position);
			}
		}
	}

	void CreateTriangle(Node a, Node b, Node c) {
		triangles.Add(a.vertexIndex);
		triangles.Add(b.vertexIndex);
		triangles.Add(c.vertexIndex);

		Triangle triangle = new Triangle(a.vertexIndex, b.vertexIndex, c.vertexIndex);
		AddTriangleToDictionary(triangle.vertexIndexA, triangle);
		AddTriangleToDictionary(triangle.vertexIndexB, triangle);
		AddTriangleToDictionary(triangle.vertexIndexC, triangle);
	}

	void AddTriangleToDictionary(int vertexIndexKey, Triangle triangle) {
		if(triangleDict.ContainsKey(vertexIndexKey)) {
			triangleDict[vertexIndexKey].Add(triangle);
		} else {
			List<Triangle> triangleList = new List<Triangle>();
			triangleList.Add(triangle);
			triangleDict.Add(vertexIndexKey, triangleList);
		}
	}

	void CalculateMeshOutlines() {
		for(int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++) {
			if(!checkedVertices.Contains(vertexIndex)) {
				int newOutlineVertex = GetConnectedOutlineVertex(vertexIndex);
				if(newOutlineVertex != -1) {
					checkedVertices.Add(vertexIndex);

					List<int> newOutline = new List<int>();
					newOutline.Add(vertexIndex);
					outlines.Add(newOutline);
					FollowOutline(newOutlineVertex, outlines.Count - 1);
					outlines[outlines.Count - 1].Add(vertexIndex);
				}
			}
		}
	}

	void FollowOutline(int vertexIndex, int outlineIndex) {
		outlines[outlineIndex].Add(vertexIndex);
		checkedVertices.Add(vertexIndex);
		int nextVertexIndex = GetConnectedOutlineVertex(vertexIndex);

		if(nextVertexIndex != -1) {
			FollowOutline(nextVertexIndex, outlineIndex);
		}
	}

	int GetConnectedOutlineVertex(int vertexIndex) {
		List<Triangle> trianglesContainingVertex = triangleDict[vertexIndex];

		foreach(Triangle triangle in trianglesContainingVertex) {
			for(int i = 0; i < 3; i++) {
				int vertexB = triangle[i];
				if(vertexB != vertexIndex && !checkedVertices.Contains(vertexB) && IsOutlineEdge(vertexIndex, vertexB)) {
					return vertexB;
				}
			}
		}

		return -1;
	}

	bool IsOutlineEdge(int vertexA, int vertexB) {
		int shared = 0;
		foreach(Triangle triangleA in triangleDict[vertexA]) {
			if(triangleA.Contains(vertexB)) {
				shared++;
				if(shared > 1) break;
			}
		}
		return shared == 1;
	}

	struct Triangle {
		public int vertexIndexA;
		public int vertexIndexB;
		public int vertexIndexC;
		int[] vertices;

		public Triangle(int a, int b, int c) {
			vertexIndexA = a;
			vertexIndexB = b;
			vertexIndexC = c;

			vertices = new int[3];
			vertices[0] = a;
			vertices[1] = b;
			vertices[2] = c;
		}

		public int this[int i] {
			get {
				return vertices[i];
			}
		}

		public bool Contains(int vertexIndex) {
			return vertexIndex == vertexIndexA || vertexIndex == vertexIndexB || vertexIndex == vertexIndexC;
		}
	}

	public class SquareGrid {
		public Square[,] squares;

		public SquareGrid(int[,] map, float squareSize) {
			int nodeCountX = map.GetLength(0);
			int nodeCountY = map.GetLength(1);
			float mapWidth = nodeCountX * squareSize;
			float mapHeight = nodeCountY * squareSize;

			ControlNode[,] controlNodes = new ControlNode[nodeCountX, nodeCountY];

			for(int x = 0; x < nodeCountX; x++) {
				for(int y = 0; y < nodeCountY; y++) {
					Vector3 pos = new Vector3(-mapWidth / 2 + x * squareSize + squareSize / 2, 0, -mapHeight / 2 + y * squareSize + squareSize / 2);
					controlNodes[x, y] = new ControlNode(pos, map[x, y] == 1, squareSize);
				}
			}

			squares = new Square[nodeCountX-1, nodeCountY-1];
			for (int x = 0; x < nodeCountX-1; x++) {
				for (int y = 0; y < nodeCountY-1; y++) {
					squares[x, y] = new Square(controlNodes[x, y + 1], controlNodes[x + 1, y + 1], controlNodes[x + 1, y], controlNodes[x, y]);
				}
			}
		}
	}

	public class Square {
		public ControlNode topLeft, topRight, botRight, botLeft;
		public Node topMid, rightMid, botMid, leftMid;
		public int configuration;

		public Square(ControlNode _topLeft, ControlNode _topRight, ControlNode _botRight, ControlNode _botLeft) {
			topLeft = _topLeft;
			topRight = _topRight;
			botLeft = _botLeft;
			botRight = _botRight;

			topMid = topLeft.right;
			rightMid = botRight.above;
			botMid = botLeft.right;
			leftMid = botLeft.above;

			if(topLeft.active) configuration += 8;
			if(topRight.active) configuration += 4;
			if(botRight.active) configuration += 2;
			if(botLeft.active) configuration += 1;
		}
	}

	public class Node {
		public Vector3 position;
		public int vertexIndex = -1;

		public Node(Vector3 _pos) {
			position = _pos;
		}
	}

	public class ControlNode : Node {
		public bool active;
		public Node above, right;
		
		public ControlNode(Vector3 _pos, bool _active, float squareSize) : base(_pos) {
			active = _active;
			above = new Node(position + Vector3.forward * squareSize / 2f);
			right = new Node(position + Vector3.right * squareSize / 2f);
		}
	}
}
