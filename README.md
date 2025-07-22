<div align="center">
<h1>CEDRL: Simulating Diverse Crowds with </br>Example-Driven Deep Reinforcement Learning</h1>
<strong><a href="https://www.apanayiotou.com/" target="_blank">Andreas Panayiotou</a>, <a href="http://www.andreasaristidou.com/" target="_blank">Andreas Aristidou</a>, and <a href="https://totis77.github.io/" target="_blank">Panayiotis Charalambous</a>

[EUROGRAPHICS 2025] Computer Graphics Forum, Volume 44, Issue 2., May 2025</br>
</div>

![Demo Image](https://github.com/veupnea/CEDRL/blob/main/Images/teaser.png)

<p align="justify">
The level of realism in virtual crowds is strongly affected by the presence of diverse crowd behaviors. In real life, we can observe various scenarios, ranging from pedestrians moving on a shopping street, people talking in static groups, or wandering around in a public park. Most of the existing systems optimize for specific behaviors such as goal-seeking and collision avoidance, neglecting to consider other complex behaviors that are usually challenging to capture or define. Departing from the conventional use of Supervised Learning, which requires vast amounts of labeled data and often lacks controllability, we introduce Crowds using Example-driven Deep Reinforcement Learning (CEDRL), a framework that simultaneously leverages multiple crowd datasets to model a broad spectrum of human behaviors. This approach enables agents to adaptively learn and exhibit diverse behaviors, enhancing their ability to generalize decisions across unseen states. The model can be applied to populate novel virtual environments while providing real-time controllability over the agents’ behaviors. We achieve this through the design of a reward function aligned with real-world observations and by employing curriculum learning that gradually diminishes the agents’ observation space. A complexity characterization metric defines each agent’s high-level crowd behavior, linking it to the agent’s state and serving as an input to the policy network. Additionally, a parametric reward function, influenced by the type ,of crowd task, facilitates the learning of a diverse and abstract behavior “skill” set. We evaluate our model on both training and unseen real-world data, comparing against other simulators, showing its ability to generalize across scenarios and accurately reflect the observed complexity of behaviors. We also examine our system’s controllability by adjusting the complexity weight, discovering that higher values lead to more complex behaviors such as wandering, static interactions, and group dynamics like joining or leaving. Finally, we demonstrate our model’s capabilities in novel synthetic scenarios.
</p>

<br>

<p align="center"><strong>
	- <a href="https://doi.org/10.1111/cgf.70015" target="_blank">Publication</a> | <a href="https://github.com/veupnea/CEDRL/blob/main/PDF%20Files/eg25-CEDRL.pdf" target="_blank">PDF Paper</a> | <a href="https://github.com/veupnea/CEDRL/blob/main/PDF%20Files/eg25-CEDRL-Supp.pdf" target="_blank">Supplementary Material (PDF)</a> | <a href="https://youtu.be/Z_zhc2Y5VWU" target="_blank">Video</a> -
</strong>
</p>

<p align="center"><strong>
 <a href="https://veupnea.github.io/publication_pages/eg25-cedrl.html" target="_blank">Project Page at VEUPNEA Website
</strong></p>

<br>

<p align="center" dir="auto">
	<a href="https://youtu.be/Z_zhc2Y5VWU" rel="nofollow">
		<img align="center" width="400px" src="https://github.com/veupnea/CEDRL/blob/main/Images/youtube_image.png"/>
	</a>
</p>






