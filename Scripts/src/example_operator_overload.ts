import { Color, Vector3 } from "UnityEngine";

if (module == require.main) {
    // operators
    {
        let vec1 = new Vector3(1, 2, 3);
        let vec2 = new Vector3(9, 8, 7);
        // @ts-ignore
        let vec3 = vec1 + vec2;
        // @ts-ignore
        let vec4 = vec1 + vec2;
        console.log("v1 = ", vec1);
        console.log("v2 = ", vec2);
        console.log("v1 + v2 =", vec3);
        // @ts-ignore
        console.log("v3 * 2 =", vec3 * 2);
        // @ts-ignore
        console.log("2 * v3=", 2 * vec3);
        // @ts-ignore
        console.log("v3 / 2 =", vec3 / 2);
        console.log("v3 == v4:", vec3 == vec4);
    }

    {
        let c1 = new Color(0, 0, 0, 1);
        let c2 = new Color(0.5, 0.1, 0.2, 0);
        // @ts-ignore
        let r = c2 / 3;
        print(c1, c2, r);
    }
}
