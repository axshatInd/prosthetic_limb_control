# prosthetic_limb_control

- Open the Project in Unity :

- Launch Unity Hub.
- Click on "Add" and navigate to the directory where YOUR PROJECT IS LOCATED Ex: ( c:\Users\aksha\Documents\Projects\prosthetic_limb_control ).
- Select the folder to add the project to Unity Hub.
- Open the project by clicking on it in Unity Hub.
- Install Required Packages :

- Once the project is open in Unity, go to Window > Package Manager .
- Ensure all the packages listed in Packages/manifest.json are installed. Unity should automatically resolve these, but you can manually check and install any missing packages.

- Open Command Prompt : You can do this by searching for "cmd" in the Start menu.
- Navigate to ~~ (YOUR CORRESPONDING) ~~ Project Directory :

```bash
cd c:\Users\aksha\Documents\Projects\prosthetic_limb_control\HandTrackingPython
 ```

- Create a Virtual Environment : This will create a virtual environment named venv in your current directory.

```bash
python -m venv venv
 ```
- Activate the Virtual Environment : This step is necessary to ensure that the dependencies are installed in the virtual environment.

```bash
venv\Scripts\activate
 ```
 
 - Install Required Packages :
Since the requirements.txt file is missing, you can manually install the packages used in your project. Based on the code snippets, you need to install opencv-python and mediapipe . Run the following command:

```bash
pip install opencv-python mediapipe
 ```
- Upgrade pip :
You can upgrade pip to the latest version using the following command:

```bash
venv\Scripts\python.exe -m pip install --upgrade pip
 ```


If you get the error message indicating that cv2 module is not found, it means that the OpenCV library is not installed in your system. You can solve the problem by :
1. Activate the Virtual Environment :
   Open your Command Prompt and navigate to your project directory, then activate the virtual environment: (CHANGE TO YOUR CORRESPONSING FILE PATH)
   
   ```bash
   cd c:\Users\aksha\Documents\Projects\prosthetic_limb_control\HandTrackingPython
   venv\Scripts\activate
    ```
2. Install OpenCV :
   Once the virtual environment is activated, install the OpenCV library using pip:
   
   ```bash
   pip install opencv-python
    ```
3. Verify Installation :
   After installation, you can verify that OpenCV is installed correctly by running your script again: (CHANGE IT ACCORDING TO YOUR CORRESPONDING PATH, BELOW WAS MINE)
   
   ```bash
   python -u "c:\Users\aksha\Documents\Projects\prosthetic_limb_control\HandTrackingPython\hand_tracking.py"
    ```
